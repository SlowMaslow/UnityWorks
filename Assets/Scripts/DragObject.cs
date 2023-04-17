using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DragObject : MonoBehaviour
{
    private Rigidbody rb;
    [HideInInspector] public Rigidbody[] padsRB = new Rigidbody[2];
    private CollisionChecker collisionChecker;
    private float mouseZCoord;
    private float fixedZPosition;
    private CharacterJoint[] padsJoints = new CharacterJoint[2];
    [SerializeField] private Transform[] padsPositions;
    [SerializeField] private Transform head;
    private SoftJointLimit minSwing2Limit, maxSwing2Limit;
    private TimeManager timeManager;


    private void Start()
    {
        collisionChecker = FindObjectOfType<CollisionChecker>();
        timeManager = FindObjectOfType<TimeManager>();
        rb = GetComponent<Rigidbody>();
        fixedZPosition = transform.position.z;
        int i;
        for (i = 0; i < 2; i++)
        {
            padsRB[i] = padsPositions[i].gameObject.GetComponent<Rigidbody>();
            padsJoints[i] = padsPositions[i].gameObject.GetComponent<CharacterJoint>();
        }
        minSwing2Limit.limit = 0;
        maxSwing2Limit.limit = 90;
    }

    private void Update()
    {
        if (padsJoints[0] != null)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, fixedZPosition);
            BreakJointCheck();
        }
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
        if (!timeManager.Paused)
        {
            rb.constraints = RigidbodyConstraints.None | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            if (PadsDistanceCheck() > 1.3)
            {
                rb.velocity = (GetMouseAsWorldPoint() - transform.position) * (5 / PadsDistanceCheck());
            }
            else
            {
                rb.velocity = (GetMouseAsWorldPoint() - transform.position) * 10;
            }
            LookerAtPad();
        }
    }

    private void BreakJointCheck()
    {
        if (PadsDistanceCheck() > 1.8f)
        {
            for (int i = 0; i < 2; i++)
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

    public void OneHandLimitCheck()
    {
        if (collisionChecker.collideCheck[0] == false && collisionChecker.collideCheck[1] == true)
        {
            padsRB[1].drag = 10000;
        }
        else
        {
            padsRB[1].drag = 5;
        }
        if (collisionChecker.collideCheck[1] == false && collisionChecker.collideCheck[0] == true)
        {
            padsRB[0].drag = 10000;
        }
        else
        {
            padsRB[0].drag = 5;
        }
    }
}
