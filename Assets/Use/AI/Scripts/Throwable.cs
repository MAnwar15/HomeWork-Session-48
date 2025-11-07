using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Throwable : MonoBehaviour
{
    public float baseNoiseRadius = 4f;      // radius for a medium hit
    public float minImpactSpeed = 1f;       // ignore tiny taps
    public float maxImpactSpeed = 15f;      // speed that maps to max noise
    public float maxNoiseMultiplier = 2f;   // radius multiplier at top speed

    Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    void OnCollisionEnter(Collision collision)
    {
        float speed = rb.linearVelocity.magnitude;
        if (speed < minImpactSpeed) return;

        float t = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, speed); // 0..1
        float radius = baseNoiseRadius * (1f + maxNoiseMultiplier * t);
        float intensity = Mathf.Clamp01(t);

        if (NoiseManager.Instance != null)
            NoiseManager.Instance.MakeNoise(transform.position, radius, intensity);

        // optional: destroy glass bottle on impact, spawn particles, etc.
    }
}
