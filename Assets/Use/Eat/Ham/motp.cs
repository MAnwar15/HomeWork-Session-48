using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    private Collider _collider;

    void Start()
    {
        _collider = GetComponent<Collider>();
        if (_collider != null)
            _collider.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        Consumable consumable = other.GetComponent<Consumable>();
        if (consumable != null && !consumable.isFinished)
        {
            consumable.Consume();
        }
    }
}
