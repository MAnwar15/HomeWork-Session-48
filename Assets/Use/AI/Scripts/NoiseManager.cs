using UnityEngine;

public class NoiseManager : MonoBehaviour
{
    public static NoiseManager Instance;
    [Tooltip("Layer(s) that contain AI colliders (usually 'AI' layer)")]
    public LayerMask aiLayer;

    void Awake()
    {
        Instance = this;
    }

    // Make a noise at position with radius (meters) and intensity (0..1)
    public void MakeNoise(Vector3 position, float radius, float intensity = 1f)
    {
        Collider[] hits = Physics.OverlapSphere(position, radius, aiLayer);
        foreach (Collider c in hits)
        {
            AIController ai = c.GetComponentInParent<AIController>();
            if (ai != null) ai.OnNoiseHeard(position, intensity, radius);
        }
    }
}
