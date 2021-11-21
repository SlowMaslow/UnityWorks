using UnityEngine;

public class NearAttack : MonoBehaviour
{
    [SerializeField] private GameObject DamageCollider;
    private Animator anim;
    private void Start()
    {
        anim = GetComponent<Animator>();
    }
    public void DamageColliderTrue()
    {
        DamageCollider.SetActive(true);
    }
    public void DamageColliderFalse()
    {
        DamageCollider.SetActive(false);
    }

    public void isDamage()
    {
        anim.SetBool("isDamage", false);
    }
}
