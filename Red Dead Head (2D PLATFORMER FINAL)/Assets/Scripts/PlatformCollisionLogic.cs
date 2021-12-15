using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformCollisionLogic : MonoBehaviour
{
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.name.Equals("PlatformCollider"))
        {
            gameObject.transform.parent = collision.transform;
        }
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.name.Equals("PlatformCollider"))
        {
            gameObject.transform.parent = null;
        }
    }
}
