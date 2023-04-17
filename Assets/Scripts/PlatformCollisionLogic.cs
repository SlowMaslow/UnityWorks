using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformCollisionLogic : MonoBehaviour
{
    private DragObject dragObject;
    private CollisionChecker collisionChecker;
    private bool currectCheck;
    private void Start()
    {
        dragObject = FindObjectOfType<DragObject>();
        collisionChecker = FindObjectOfType<CollisionChecker>();
    }
    private void OnCollisionStay(Collision collision)
    {
        collision.gameObject.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
        if (collision.gameObject.GetComponent<Rigidbody>() == collision.gameObject.GetComponent<DragObject>().padsRB[0])
        {
            collisionChecker.collideCheck[0] = true;
        }
        else
        {
            collisionChecker.collideCheck[1] = true;
        }
        dragObject.OneHandLimitCheck();
    }
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.GetComponent<Rigidbody>() == collision.gameObject.GetComponent<DragObject>().padsRB[0])
        {
            collisionChecker.collideCheck[0] = false;
        }
        else
        {
            collisionChecker.collideCheck[1] = false;
        }
    }
}
