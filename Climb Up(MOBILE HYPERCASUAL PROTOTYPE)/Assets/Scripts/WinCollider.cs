using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WinCollider : MonoBehaviour
{
    [SerializeField] private WinScript winScript;
    [SerializeField] private Collider Pad;
    private void OnTriggerEnter(Collider other)
    {
        if(other == Pad)
        {
            winScript.WinValue++;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other == Pad)
        {
            winScript.WinValue--;
        }
    }
}
