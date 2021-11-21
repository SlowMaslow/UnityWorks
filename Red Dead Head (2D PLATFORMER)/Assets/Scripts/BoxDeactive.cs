using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxDeactive : MonoBehaviour
{
    [SerializeField] GameObject box;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject == box)
        {
            box.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.None;
        }
    }
}
