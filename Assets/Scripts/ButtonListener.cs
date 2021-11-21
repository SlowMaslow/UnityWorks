using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonListener : MonoBehaviour
{
    private bool isActive;
    private Animator anim;
    [SerializeField] GameObject Wall;
    private void Start()
    {
        anim = GetComponent<Animator>();
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive)
        {
            Wall.SetActive(false);
            anim.SetBool("isActive", true);
            isActive = true;
        }
    }
}
