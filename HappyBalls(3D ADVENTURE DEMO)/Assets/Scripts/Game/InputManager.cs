using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class InputManager : MonoBehaviour
{
    public float x;
    public float z;

    // Update is called once per frame
    void Update()
    {
        x = Input.GetAxis("Vertical");
        z = Input.GetAxis("Horizontal");
    }
}
