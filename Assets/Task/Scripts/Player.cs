using UnityEngine;

public class Player : MonoBehaviour
{
    public float Health, MaxHealth;

    [SerializeField]
    private HealthBar healthBar;

    void Start()
    {
        healthBar.SetMaxHealth(MaxHealth);
    }

    void Update()
    {
        
    }
    public void SetHealth(float healthchange)
    {
        Health += healthchange;
        Health = Mathf.Clamp(Health, 0, MaxHealth);

        healthBar.SetHealth(Health);
    }
}
