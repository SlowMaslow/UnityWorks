using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformCollisionLogic : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        collision.gameObject.GetComponent<Rigidbody>().isKinematic = true;
    }
}
