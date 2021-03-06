using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform target;

    void Update()
    {
        transform.position = new Vector3(target.position.x, target.position.y + 1, transform.position.z);
    }
}
