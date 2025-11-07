using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
public class AIController : MonoBehaviour
{
    public enum State { Patrol, Suspicious, Investigate, Alert, Chase, ReturnToPatrol }
    public State currentState = State.Patrol;

    [Header("References")]
    public Transform[] patrolPoints;
    public Transform eyes; // child at eye height
    public Transform player; // player's head (HMD) or a target transform
    public LayerMask obstacleMask; // layers considered obstacles (set in inspector)

    [Header("Nav & Patrol")]
    NavMeshAgent agent;
    int patrolIndex = 0;
    public float patrolStopDelay = 1.0f;

    [Header("Vision")]
    public float sightRange = 12f;
    [Range(0, 360)] public float fieldOfView = 110f;
    public float detectionThreshold = 2.0f; // seconds to fully detect player
    private float detectionProgress = 0f;
    public float detectionGain = 1.2f;
    public float detectionLose = 1.0f;

    [Header("Hearing / Investigation")]
    public float investigateStopDistance = 0.6f;
    public float investigateLookDuration = 3f;
    public float suspiciousLookDuration = 1.5f;
    public float loudNoiseAlertThreshold = 0.9f; // intensity above this = immediate alert

    // --- Added for new Suspicious logic ---
    public float suspiciousDelay = 3f;
    private float suspiciousTimer = 0f;

    Vector3 investigatePosition;
    Coroutine investigateRoutine;

    Vector3 lastKnownPlayerPos;
    float lostSightTimer = 0f;
    public float loseSightTime = 4f;
    public float maxChaseDistance = 25f;

    // cache colliders on self to ignore in raycasts
    Collider[] selfColliders;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (eyes == null) eyes = transform;
        agent.updateRotation = false; // we'll control rotation manually

        selfColliders = GetComponentsInChildren<Collider>();
    }

    void Start()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[0].position);
    }

    void Update()
    {
        VisionCheck();

        switch (currentState)
        {
            case State.Patrol:
                PatrolUpdate();
                // New logic: transition to Suspicious if player seen
                if (CanSeePlayer())
                {
                    currentState = State.Suspicious;
                    suspiciousTimer = suspiciousDelay;
                }
                break;

            case State.Suspicious:
                SuspiciousUpdate();
                break;

            case State.Investigate: InvestigateUpdate(); break;
            case State.Alert: AlertUpdate(); break;
            case State.Chase: ChaseUpdate(); break;
        }
    }

    // --- New Suspicious state logic ---
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
            // Lost sight of player before timer finished
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

        // 1) RaycastAll up to player distance and inspect first non-self hit.
        RaycastHit[] hits = Physics.RaycastAll(eyes.position, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                // ignore hits that belong to this AI
                if (IsHitSelf(h.collider)) continue;

                // if first non-self hit is part of the player -> visible
                if (h.collider.transform == player || h.collider.transform.IsChildOf(player))
                    return true;

                // otherwise something else is blocking
                return false;
            }
        }

        // 2) If RaycastAll found nothing (or only self), check obstacles layers only:
        //    if there is any obstacle between eyes and player on obstacleMask -> occluded
        if (Physics.Raycast(eyes.position, dir.normalized, out RaycastHit obstacleHit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false; // obstacle in the way
        }

        // No obstacle and no collider hit -> treat as visible (useful for HMD without collider)
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

        // rotate toward movement direction for nicer visuals
        ApplyAgentRotation();

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f && !waitingAtPoint)
        {
            StartCoroutine(AdvancePatrolPoint());
        }
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

    #region Investigate / Suspicious / Chase / Alert
    public void OnNoiseHeard(Vector3 noisePos, float intensity, float radius)
    {
        if (currentState == State.Alert || currentState == State.Chase)
            return;

        investigatePosition = noisePos;

        if (intensity >= loudNoiseAlertThreshold)
        {
            // go to the noise quickly and act suspicious (Alert state)
            currentState = State.Alert;
            agent.SetDestination(investigatePosition);
        }
        else
        {
            currentState = State.Investigate;
            agent.SetDestination(investigatePosition);

            if (investigateRoutine != null)
                StopCoroutine(investigateRoutine);
            investigateRoutine = StartCoroutine(InvestigateCoroutine());
        }
    }

    IEnumerator InvestigateCoroutine()
    {
        float timeout = 10f;
        float timer = 0f;

        // wait for arrival
        while ((agent.pathPending || agent.remainingDistance > investigateStopDistance) && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        // look around
        float lookTimer = 0f;
        while (lookTimer < investigateLookDuration)
        {
            LookAround();
            lookTimer += Time.deltaTime;
            yield return null;
        }

        ReturnToPatrol();
    }

    // Shorter investigate behavior used by Alert (louder noise)
    IEnumerator AlertInvestigateCoroutine()
    {
        float lookTimer = 0f;
        while (lookTimer < suspiciousLookDuration)
        {
            LookAround();
            lookTimer += Time.deltaTime;
            yield return null;
        }

        // after short suspicious look, either continue investigating or return to patrol
        ReturnToPatrol();
    }

    void InvestigateUpdate()
    {
        // rotate to face the investigate position while moving
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
        // once reached alert destination, perform short suspicious look
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (investigateRoutine != null) StopCoroutine(investigateRoutine);
            investigateRoutine = StartCoroutine(AlertInvestigateCoroutine());
            currentState = State.Suspicious; // temporary state while looking
        }
    }

    void LookAround()
    {
        float turnSpeed = 60f;
        transform.Rotate(0, Mathf.Sin(Time.time * 2f) * turnSpeed * Time.deltaTime, 0);
    }

    void ChaseUpdate()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (CanSeePlayer())
        {
            lastKnownPlayerPos = player.position;
            lostSightTimer = 0f;
            agent.SetDestination(player.position);

            // rotate to face player (smooth)
            Vector3 dirToPlayer = player.position - transform.position;
            dirToPlayer.y = 0;
            if (dirToPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion rot = Quaternion.LookRotation(dirToPlayer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 6f);
            }
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

    void ReturnToPatrol()
    {
        currentState = State.Patrol;
        detectionProgress = 0f;
        if (patrolPoints != null && patrolPoints.Length > 0)
            agent.SetDestination(patrolPoints[patrolIndex].position);
    }
    #endregion

    #region Utilities
    void ApplyAgentRotation()
    {
        // rotate to face the agent's desired velocity so movement looks natural
        Vector3 v = agent.desiredVelocity;
        v.y = 0;
        if (v.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(v.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 6f);
        }
    }
    #endregion

    #region Debugging Gizmos
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
            }
        }
    }
    #endregion
}
