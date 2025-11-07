using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
public class AIController : MonoBehaviour
{
    public enum State { Patrol, Suspicious, Investigate, Alert, Chase, ReturnToPatrol, Attack }
    public State currentState = State.Patrol;

    [Header("References")]
    public Transform[] patrolPoints;
    public Transform eyes;
    public Transform player;
    public LayerMask obstacleMask;

    [Header("Nav & Patrol")]
    NavMeshAgent agent;
    int patrolIndex = 0;
    public float patrolStopDelay = 1.0f;

    [Header("Vision")]
    public float sightRange = 12f;
    [Range(0, 360)] public float fieldOfView = 110f;
    public float detectionThreshold = 2.0f;
    private float detectionProgress = 0f;
    public float detectionGain = 1.2f;
    public float detectionLose = 1.0f;

    [Header("Hearing / Investigation")]
    public float investigateStopDistance = 0.6f;
    public float investigateLookDuration = 3f;
    public float suspiciousLookDuration = 1.5f;
    public float loudNoiseAlertThreshold = 0.9f;

    [Header("Suspicious")]
    public float suspiciousDelay = 3f;
    private float suspiciousTimer = 0f;

    [Header("Combat")]
    public float attackRange = 2f;      // how close before attacking
    public float attackDamage = 10f;    // damage per hit
    public float attackCooldown = 1.5f; // seconds between attacks
    private float attackTimer = 0f;
    private Player playerScript;        // cached Player component

    Vector3 investigatePosition;
    Coroutine investigateRoutine;

    Vector3 lastKnownPlayerPos;
    float lostSightTimer = 0f;
    public float loseSightTime = 4f;
    public float maxChaseDistance = 25f;

    Collider[] selfColliders;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (eyes == null) eyes = transform;
        agent.updateRotation = false;
        selfColliders = GetComponentsInChildren<Collider>();
    }

    void Start()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[0].position);

        if (player != null)
            playerScript = player.GetComponent<Player>();
    }

    void Update()
    {
        attackTimer -= Time.deltaTime;

        VisionCheck();

        switch (currentState)
        {
            case State.Patrol:
                PatrolUpdate();
                if (CanSeePlayer())
                {
                    currentState = State.Suspicious;
                    suspiciousTimer = suspiciousDelay;
                }
                break;

            case State.Suspicious:
                SuspiciousUpdate();
                break;

            case State.Investigate:
                InvestigateUpdate();
                break;

            case State.Alert:
                AlertUpdate();
                break;

            case State.Chase:
                ChaseUpdate();
                break;

            case State.Attack:
                AttackUpdate();
                break;
        }
    }

    void SuspiciousUpdate()
    {
        if (CanSeePlayer())
        {
            suspiciousTimer -= Time.deltaTime;
            if (suspiciousTimer <= 0f)
            {
                currentState = State.Chase;
            }
        }
        else
        {
            currentState = State.Patrol;
        }
    }

    #region Vision
    void VisionCheck()
    {
        if (player == null) return;

        if (CanSeePlayer())
        {
            detectionProgress += detectionGain * Time.deltaTime;
            detectionProgress = Mathf.Min(detectionProgress, detectionThreshold);

            if (detectionProgress >= detectionThreshold)
                GoAlert();
        }
        else
        {
            detectionProgress = Mathf.Max(0f, detectionProgress - detectionLose * Time.deltaTime);
        }
    }

    bool CanSeePlayer()
    {
        if (player == null || eyes == null) return false;

        Vector3 dir = player.position - eyes.position;
        float dist = dir.magnitude;
        if (dist > sightRange) return false;

        float angle = Vector3.Angle(eyes.forward, dir.normalized);
        if (angle > fieldOfView * 0.5f) return false;

        RaycastHit[] hits = Physics.RaycastAll(eyes.position, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (IsHitSelf(h.collider)) continue;

                if (h.collider.transform == player || h.collider.transform.IsChildOf(player))
                    return true;

                return false;
            }
        }

        if (Physics.Raycast(eyes.position, dir.normalized, out RaycastHit obstacleHit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return true;
    }

    bool IsHitSelf(Collider c)
    {
        if (c == null) return false;
        foreach (var col in selfColliders)
            if (col == c) return true;
        return false;
    }
    #endregion

    #region Patrol
    bool waitingAtPoint = false;

    void PatrolUpdate()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        ApplyAgentRotation();

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f && !waitingAtPoint)
            StartCoroutine(AdvancePatrolPoint());
    }

    IEnumerator AdvancePatrolPoint()
    {
        waitingAtPoint = true;
        yield return new WaitForSeconds(patrolStopDelay);
        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        agent.SetDestination(patrolPoints[patrolIndex].position);
        waitingAtPoint = false;
    }
    #endregion

    #region Investigation / Alert / Chase / Attack
    void InvestigateUpdate()
    {
        Vector3 lookDir = (investigatePosition - transform.position);
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(lookDir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 4f);
        }
    }

    void AlertUpdate()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (investigateRoutine != null) StopCoroutine(investigateRoutine);
            investigateRoutine = StartCoroutine(AlertInvestigateCoroutine());
            currentState = State.Suspicious;
        }
    }

    void ChaseUpdate()
    {
        if (player == null) return;
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= attackRange)
        {
            currentState = State.Attack;
            agent.ResetPath();
            return;
        }

        if (CanSeePlayer())
        {
            lastKnownPlayerPos = player.position;
            lostSightTimer = 0f;
            agent.SetDestination(player.position);
            RotateTowards(player.position);
        }
        else
        {
            lostSightTimer += Time.deltaTime;
            if (lostSightTimer > loseSightTime || distance > maxChaseDistance)
            {
                currentState = State.Investigate;
                agent.SetDestination(lastKnownPlayerPos);

                if (investigateRoutine != null) StopCoroutine(investigateRoutine);
                investigateRoutine = StartCoroutine(InvestigateCoroutine());
                return;
            }
        }
    }

    void AttackUpdate()
    {
        if (player == null) return;
        float distance = Vector3.Distance(transform.position, player.position);
        RotateTowards(player.position);

        if (distance > attackRange + 0.5f)
        {
            currentState = State.Chase;
            return;
        }

        if (attackTimer <= 0f)
        {
            attackTimer = attackCooldown;
            AttackPlayer();
        }
    }

    void AttackPlayer()
    {
        if (playerScript != null)
        {
            playerScript.SetHealth(-attackDamage); // subtract health
            Debug.Log($"{name} attacked player for {attackDamage} damage!");
        }
    }

    void RotateTowards(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion rot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 6f);
        }
    }

    void GoAlert()
    {
        currentState = State.Chase;
        lostSightTimer = 0f;
        if (player != null)
        {
            lastKnownPlayerPos = player.position;
            agent.SetDestination(player.position);
        }
    }
    #endregion

    #region Coroutines
    IEnumerator InvestigateCoroutine()
    {
        float timeout = 10f;
        float timer = 0f;

        while ((agent.pathPending || agent.remainingDistance > investigateStopDistance) && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        float lookTimer = 0f;
        while (lookTimer < investigateLookDuration)
        {
            LookAround();
            lookTimer += Time.deltaTime;
            yield return null;
        }

        ReturnToPatrol();
    }

    IEnumerator AlertInvestigateCoroutine()
    {
        float lookTimer = 0f;
        while (lookTimer < suspiciousLookDuration)
        {
            LookAround();
            lookTimer += Time.deltaTime;
            yield return null;
        }
        ReturnToPatrol();
    }
    #endregion

    #region Utilities
    void LookAround()
    {
        float turnSpeed = 60f;
        transform.Rotate(0, Mathf.Sin(Time.time * 2f) * turnSpeed * Time.deltaTime, 0);
    }

    void ReturnToPatrol()
    {
        currentState = State.Patrol;
        detectionProgress = 0f;
        if (patrolPoints != null && patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[patrolIndex].position);
    }

    void ApplyAgentRotation()
    {
        Vector3 v = agent.desiredVelocity;
        v.y = 0;
        if (v.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(v.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 6f);
        }
    }
    #endregion

    #region Debug Gizmos
    void OnDrawGizmosSelected()
    {
        if (eyes != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(eyes.position, sightRange);

            Vector3 forward = eyes.forward;
            Quaternion leftRot = Quaternion.Euler(0, -fieldOfView * 0.5f, 0);
            Quaternion rightRot = Quaternion.Euler(0, fieldOfView * 0.5f, 0);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(eyes.position, eyes.position + leftRot * forward * sightRange);
            Gizmos.DrawLine(eyes.position, eyes.position + rightRot * forward * sightRange);

            if (player != null)
            {
                Gizmos.color = CanSeePlayer() ? Color.green : Color.red;
                Gizmos.DrawLine(eyes.position, player.position);
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, attackRange);
            }
        }
    }
    #endregion
}
