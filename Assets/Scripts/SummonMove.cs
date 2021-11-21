using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SummonMove : MonoBehaviour
{

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.position = new Vector2(transform.position.x - 0.025f, transform.position.y);
    }
}
