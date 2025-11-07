using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRBaseInteractable))]
public class VRButton : MonoBehaviour
{
    [Header("Button Settings")]
    public UnityEvent onPress;
    public AudioClip sfx;
    public float pressDepth = 0.012f;
    public float animSpeed = 12f;

    private Vector3 restLocal;
    private Vector3 pressedLocal;
    private bool animating = false;
    private AudioSource sfxSource;
    private XRBaseInteractable interactable;

    void Awake()
    {
        restLocal = transform.localPosition;
        pressedLocal = restLocal + Vector3.down * pressDepth;

        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null && sfx != null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.spatialBlend = 1f;
            sfxSource.playOnAwake = false;
        }

        interactable = GetComponent<XRBaseInteractable>();
        interactable.selectEntered.AddListener(_ => Press());
    }

    public void Press()
    {
        if (animating) return;
        StartCoroutine(DoPress());
    }

    private IEnumerator DoPress()
    {
        animating = true;
        if (sfx && sfxSource) sfxSource.PlayOneShot(sfx);
        onPress?.Invoke();

        // Animate press down
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * animSpeed;
            transform.localPosition = Vector3.Lerp(restLocal, pressedLocal, t);
            yield return null;
        }

        yield return new WaitForSeconds(0.06f);

        // Animate release
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * animSpeed;
            transform.localPosition = Vector3.Lerp(pressedLocal, restLocal, t);
            yield return null;
        }

        animating = false;
    }
}
