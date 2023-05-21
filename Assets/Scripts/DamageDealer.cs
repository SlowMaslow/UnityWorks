using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [SerializeField] private float damage;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Damageble"))
        {
            if (!collision.GetComponent<Health>().Invisible)
            {
                collision.GetComponent<Health>().TakeDamage(damage);
                collision.GetComponent<Health>().Invisible = true;
                if (transform.position.x > collision.transform.position.x)
                {
                    if (collision.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static)
                        collision.GetComponent<Rigidbody2D>().velocity = new Vector2(-1, 3);
                }
                else
                {
                    if (collision.GetComponent<Rigidbody2D>().bodyType != RigidbodyType2D.Static)
                        collision.GetComponent<Rigidbody2D>().velocity = new Vector2(1, 3);
                }
                collision.GetComponent<Animator>().SetBool("isDamage", true);
            }
        }
        if (gameObject.layer != 16)
        {
            Destroy(gameObject);
        }          
    }
}
