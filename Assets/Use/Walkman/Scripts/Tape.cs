using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Tape : MonoBehaviour
{
    [Tooltip("Audio that this cassette contains.")]
    public AudioClip tapeClip;

    [Tooltip("Optional name for editor.")]
    public string tapeName;

    // Helper if you need it
    public void MakeKinematic()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
    }
    public void MakePhysical()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = false;
    }
}
