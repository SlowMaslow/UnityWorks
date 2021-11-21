using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StopColliderScript : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.name.Equals("Wolf"))
        {
            collision.gameObject.GetComponent<WolfMove>().mover *= -1;
            collision.gameObject.GetComponent<SpriteRenderer>().flipX = !collision.gameObject.GetComponent<SpriteRenderer>().flipX;
        }
    }
}
