using UnityEngine;

public class Consumable : MonoBehaviour
{
    [SerializeField] private GameObject[] portions;
    [SerializeField] private int index = 0;

    public bool isFinished => index >= portions.Length;

    private AudioSource _audioSource;

    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource != null)
            _audioSource.playOnAwake = false;
    }

    private void OnValidate()
    {
        SetVisuals();
    }

    [ContextMenu("Consume")]
    public void Consume()
    {
        if (!isFinished)
        {
            index++;
            SetVisuals();
            if (_audioSource != null)
                _audioSource.Play();
        }
    }

    private void SetVisuals()
    {
        for (int i = 0; i < portions.Length; i++)
        {
            portions[i].SetActive(i == index);
        }
    }
}
