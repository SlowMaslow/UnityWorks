using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DragObject : MonoBehaviour
{
    private Rigidbody rb;
    private float mouseZCoord;
    private float fixedZPosition;
    private CharacterJoint[] padsJoints = new CharacterJoint[2];
    [SerializeField] private Transform[] padsPositions;
    [SerializeField] private Transform head;
    private SoftJointLimit minSwing2Limit, maxSwing2Limit;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        fixedZPosition = transform.position.z;
        int i;
        for (i = 0; i < 2; i++)
        {
            padsJoints[i] = padsPositions[i].gameObject.GetComponent<CharacterJoint>();
        }
        minSwing2Limit.limit = 0;
        maxSwing2Limit.limit = 90;
    }

    private void Update()
    {
        transform.position = new Vector3(transform.position.x, transform.position.y, fixedZPosition);
        BreakJointCheck();
    }

    private Vector3 GetMouseAsWorldPoint()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = mouseZCoord;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }

    void OnMouseDown()
    {
        mouseZCoord = Camera.main.WorldToScreenPoint(transform.position).z;
    }

    void OnMouseUp()
    {
        head.GetComponent<CharacterJoint>().swing2Limit = minSwing2Limit;
    }

    void OnMouseDrag()
    {      
        rb.isKinematic = false;
        if(PadsDistanceCheck() > 1.3)
        {
            rb.velocity = (GetMouseAsWorldPoint() - transform.position) * (5 / PadsDistanceCheck());
        }
        else
        {
            rb.velocity = (GetMouseAsWorldPoint() - transform.position) * 10;
        }
        LookerAtPad();
    }

    private void BreakJointCheck()
    {
        if (PadsDistanceCheck() > 1.8f)
        {
            int i;
            for (i = 0; i < 2; i++)
            {
                padsJoints[i].breakForce = 0;
            }
        }
    }

    private void LookerAtPad()
    {
        head.GetComponent<CharacterJoint>().swing2Limit = maxSwing2Limit;
        head.LookAt(transform.position);
    }

    private float PadsDistanceCheck()
    {
        float padsDistance = Vector3.Distance(padsPositions[0].position, padsPositions[1].position);
        return padsDistance;
    }
}
