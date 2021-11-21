using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BreakForceScript : MonoBehaviour
{
    private FixedJoint2D fixedJoint;
    private bool isTouched;
    private void Start()
    {
        fixedJoint = GetComponent<FixedJoint2D>();
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isTouched)
        {
            fixedJoint.breakForce = 0;
            isTouched = true;
        }
        
    }
}
