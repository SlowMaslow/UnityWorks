using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReverseCamera : MonoBehaviour
{
    public Transform TargetObject;
    private Vector3 yRotation;
    public float Sens;

    private MobileCheck mobileCheck;
    private MobileButtonsList mobileButtonList;

    void Start()
    {
        yRotation = TargetObject.eulerAngles;
        mobileCheck = FindObjectOfType<MobileCheck>();
        mobileButtonList = FindObjectOfType<MobileButtonsList>();
    }

    void FixedUpdate()
    {
        if (mobileCheck.isMobile != 1)
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
        else
        {
            if (mobileButtonList.CameraJoystick.Horizontal > 0)
            {
                yRotation.y = yRotation.y + Sens;
            }
            if (mobileButtonList.CameraJoystick.Horizontal < 0)
            {
                yRotation.y = yRotation.y - Sens;
            }
            TargetObject.localEulerAngles = Vector3.Lerp(TargetObject.eulerAngles, yRotation, 1);
        }   
    }
}
