using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WolfMove : MonoBehaviour
{
    private Health health;
    public float mover = 0.025f;
    private void Start()
    {
        health = GetComponent<Health>();
    }
    void FixedUpdate()
    {
        if (health.isAlive)
        transform.position = new Vector2(transform.position.x - mover, transform.position.y);
    }
}
