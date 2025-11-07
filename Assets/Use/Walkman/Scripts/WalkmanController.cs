using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(AudioSource))]
public class WalkmanController : MonoBehaviour
{
    [Header("Audio / Slot")]
    public AudioSource playerSource;          // main playback AudioSource (spatial)
    public Transform tapeSlot;                // where tape should sit (assign GameObject at the slot)
    public Transform ejectPoint;              // optional eject direction point
    public AudioClip insertSfx;
    public AudioClip ejectSfx;
    public bool autoPlayOnInsert = true;

    [Header("Detection")]
    public float nearbyDetectRadius = 0.25f;
    public LayerMask tapeLayer;

    // runtime
    Tape currentTape;
    XRSocketInteractor socket;

    void Awake()
    {
        if (playerSource == null) playerSource = GetComponent<AudioSource>();
        if (tapeSlot != null) socket = tapeSlot.GetComponent<XRSocketInteractor>();
    }

    void OnEnable()
    {
        // Subscribe to the modern select events (SelectEnterEventArgs / SelectExitEventArgs)
        if (socket != null)
        {
            socket.selectEntered.AddListener(OnSocketSelectEntered);
            socket.selectExited.AddListener(OnSocketSelectExited);
        }
    }

    void OnDisable()
    {
        if (socket != null)
        {
            socket.selectEntered.RemoveListener(OnSocketSelectEntered);
            socket.selectExited.RemoveListener(OnSocketSelectExited);
        }
    }

    // --- Socket event callbacks (new signatures)
    public void OnSocketSelectEntered(SelectEnterEventArgs args)
    {
        // Cast to XRBaseInteractable to get GameObject (safe common case)
        var xrInteractable = args.interactableObject as XRBaseInteractable;
        if (xrInteractable == null) return;

        var go = xrInteractable.transform.gameObject;
        var tape = go.GetComponent<Tape>();
        if (tape != null) InsertTape(tape);
    }

    public void OnSocketSelectExited(SelectExitEventArgs args)
    {
        // ignore canceled selects (object destroyed/unregistered)
        if (args.isCanceled) return;

        var xrInteractable = args.interactableObject as XRBaseInteractable;
        if (xrInteractable == null) return;

        var go = xrInteractable.transform.gameObject;
        var tape = go.GetComponent<Tape>();
        if (tape != null && tape == currentTape)
        {
            ClearTapeState();
        }
    }

    // --- Manual insertion (used by the Insert/Eject button)
    public void ToggleGate()
    {
        if (currentTape != null)
        {
            EjectTape();
            return;
        }

        // look for nearby tape and snap it in
        Collider[] found = Physics.OverlapSphere(tapeSlot.position, nearbyDetectRadius, tapeLayer);
        foreach (var c in found)
        {
            Tape t = c.GetComponentInParent<Tape>();
            if (t != null)
            {
                InsertTape(t);
                return;
            }
        }

        // nothing found: play a 'no tape' or gate click SFX if assigned
        if (insertSfx != null) AudioSource.PlayClipAtPoint(insertSfx, transform.position);
    }

    public void InsertTape(Tape tape)
    {
        if (tape == null) return;

        if (currentTape != null)
        {
            // eject current first (optional)
            EjectTape();
        }

        // Parent to slot and lock physics
        tape.transform.SetParent(tapeSlot, false);
        tape.transform.localPosition = Vector3.zero;
        tape.transform.localRotation = Quaternion.identity;
        var rb = tape.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        currentTape = tape;
        playerSource.clip = tape.tapeClip;
        if (insertSfx != null) AudioSource.PlayClipAtPoint(insertSfx, transform.position);
        if (autoPlayOnInsert) Play();
    }

    public void EjectTape()
    {
        if (currentTape == null) return;

        var t = currentTape;
        // unparent and re-enable physics
        t.transform.SetParent(null, true);
        var rb = t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            // small outward impulse so it pops out
            Vector3 dir = (ejectPoint != null) ? (ejectPoint.position - t.transform.position).normalized
                                              : (transform.forward + Vector3.up * 0.2f).normalized;
            rb.AddForce(dir * 1.8f, ForceMode.VelocityChange);
        }

        if (ejectSfx != null) AudioSource.PlayClipAtPoint(ejectSfx, transform.position);

        // stop audio and clear
        playerSource.Stop();
        playerSource.clip = null;
        currentTape = null;
    }

    void ClearTapeState()
    {
        playerSource.Stop();
        playerSource.clip = null;
        currentTape = null;
    }

    // --- Playback control (Play method added)
    public void Play()
    {
        if (playerSource == null) return;
        if (playerSource.clip == null) return;

        // If it's already playing do nothing. If it was paused (time>0), UnPause;
        // otherwise Play from the start.
        if (playerSource.isPlaying) return;

        if (playerSource.time > 0f)
            playerSource.UnPause();
        else
            playerSource.Play();
    }

    public void Pause()
    {
        if (playerSource == null) return;
        if (playerSource.isPlaying) playerSource.Pause();
    }

    public void PlayPauseToggle()
    {
        if (playerSource == null || playerSource.clip == null) return;
        if (playerSource.isPlaying) Pause();
        else Play();
    }

    public void StopPlayback()
    {
        if (playerSource == null) return;
        playerSource.Stop();
        playerSource.time = 0f;
    }

    // Inspector-friendly wrappers for buttons
    public void OnPlayPausePressed() => PlayPauseToggle();
    public void OnStopPressed() => StopPlayback();
    public void OnInsertEjectPressed() => ToggleGate();
}
