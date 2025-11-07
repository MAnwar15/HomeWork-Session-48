using UnityEngine;
using System.Collections;

public class WalkmanGate : MonoBehaviour
{
    [Tooltip("Rotation (local) when closed")]
    public Vector3 closedRotation = Vector3.zero;

    [Tooltip("Rotation (local) when opened")]
    public Vector3 openRotation = new Vector3(-75f, 0f, 0f);

    [Tooltip("Speed of gate rotation")]
    public float openSpeed = 4f;

    [Tooltip("Audio for gate open")]
    public AudioClip openSfx;

    [Tooltip("Audio for gate close")]
    public AudioClip closeSfx;

    AudioSource sfxSource;
    bool isOpen = false;
    Coroutine animRoutine;

    public bool IsOpen => isOpen;

    void Start()
    {
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.spatialBlend = 1f;
            sfxSource.playOnAwake = false;
        }
    }

    public void ToggleGate()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (isOpen) return;
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateGate(openRotation, openSfx));
        isOpen = true;
    }

    public void Close()
    {
        if (!isOpen) return;
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateGate(closedRotation, closeSfx));
        isOpen = false;
    }

    IEnumerator AnimateGate(Vector3 targetRot, AudioClip sfx)
    {
        if (sfx != null) sfxSource.PlayOneShot(sfx);

        Quaternion startRot = transform.localRotation;
        Quaternion endRot = Quaternion.Euler(targetRot);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
        transform.localRotation = endRot;
    }
}
