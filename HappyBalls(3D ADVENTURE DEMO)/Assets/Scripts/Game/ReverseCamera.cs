using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReverseCamera : MonoBehaviour
{
    public Transform TargetObject;
    private Vector3 yRotation;
    public float Sens;

    void Start()
    {
        yRotation = TargetObject.eulerAngles;
    }

    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.Mouse1))
        {
            if (Input.GetAxis("Mouse X") > 0)
            {
                yRotation.y = yRotation.y + Sens;
            }
            if (Input.GetAxis("Mouse X") < 0)
            {
                yRotation.y = yRotation.y - Sens;
            }
            TargetObject.localEulerAngles = Vector3.Lerp(TargetObject.eulerAngles, yRotation, 1);
        }
    }
}
