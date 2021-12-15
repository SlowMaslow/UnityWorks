using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FailCollider : MonoBehaviour
{
    [SerializeField] private GameObject FallWindow;

    private void OnTriggerEnter(Collider other)
    {
        FallWindow.SetActive(true);
    }
}
