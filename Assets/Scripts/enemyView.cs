using UnityEngine;

public class enemyView : MonoBehaviour
{
    private Rigidbody2D rb;
    private Transform objectTransform;
    private enemyAttackTriger enemyAttackTriger;
    private Animator anim;
    private Health selfHealth;
    private void Start()
    {
        rb = GetComponentInParent<Rigidbody2D>();
        enemyAttackTriger = transform.parent.gameObject.GetComponentInChildren<enemyAttackTriger>();
        anim = transform.parent.gameObject.GetComponentInParent<Animator>();
        objectTransform = transform.parent.transform.parent.GetComponent<Transform>();
        selfHealth = transform.parent.transform.parent.GetComponent<Health>();
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        if(selfHealth.isAlive)
        {
            if (!enemyAttackTriger.isAttack)
            {
                if (collision.name.Equals("Main_Hero"))
                {
                    if (collision.transform.position.x < transform.position.x)
                    {
                        rb.velocity = new Vector2(-0.5f, rb.velocity.y);
                        objectTransform.rotation = new Quaternion(objectTransform.rotation.x, 180f, objectTransform.rotation.z, 0f);
                    }
                    else
                    {
                        rb.velocity = new Vector2(0.5f, rb.velocity.y);
                        objectTransform.rotation = new Quaternion(objectTransform.rotation.x, 0f, objectTransform.rotation.z, 0f);
                    }
                    anim.SetBool("isRun", true);
                    anim.SetBool("isAtack", false);
                }
            }
            else
            {
                anim.SetBool("isRun", false);
                anim.SetBool("isAtack", true);
            }
        }   
    }
}
