using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth;
    [SerializeField] private float currentHealth;
    [SerializeField] private float InvisibleTime;
    [HideInInspector] public bool Invisible;
    private float currentTime;
    [HideInInspector] public bool isAlive;
    private UI_HealthManager healthManager;
    private UI_DamageTextCreator damageTextCreator;
    private Animator anim;

    private void Awake()
    {
        currentHealth = maxHealth;
        isAlive = true;
        healthManager = GetComponent<UI_HealthManager>();
        damageTextCreator = GetComponentInChildren<UI_DamageTextCreator>();
        healthManager.UI_SetMaxHealth(maxHealth);
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        if (currentTime < InvisibleTime && Invisible)
        {
            currentTime += Time.deltaTime;
        }
        else
        {
            currentTime = 0;
            Invisible = false;
        }
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        healthManager.UI_HealthChanger(damage);
        damageTextCreator.DamageTextCreate(damage);
        CheckIsAlive();
    }

    private void CheckIsAlive()
    {
        if(currentHealth > 0)
        {
            isAlive = true;
        }
        else
        {
            isAlive = false;
        }
        if (!isAlive)
        {
            anim.SetBool("isAlive", false);
        }
    }

    public void Destroying()
    {
        Destroy(gameObject);
    }
    public void DestroingParent()
    {
        Destroy(gameObject.transform.parent.gameObject);
    }
}
