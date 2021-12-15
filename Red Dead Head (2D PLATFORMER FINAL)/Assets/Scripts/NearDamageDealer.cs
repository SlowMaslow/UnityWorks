using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NearDamageDealer : MonoBehaviour
{
    [SerializeField] private float damage;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Damageble"))
        {
            if (!collision.gameObject.GetComponent<Health>().Invisible)
            {
                collision.GetComponent<Health>().TakeDamage(damage);
                collision.GetComponent<Health>().Invisible = true;
                if (transform.position.x > collision.transform.position.x)
                {
                    collision.GetComponent<Rigidbody2D>().velocity = new Vector2(-1, 1);
                }
                else
                {
                    collision.GetComponent<Rigidbody2D>().velocity = new Vector2(1, 1);
                }
                collision.GetComponent<Animator>().SetBool("isDamage", true);
            } 
        }
    }
}
