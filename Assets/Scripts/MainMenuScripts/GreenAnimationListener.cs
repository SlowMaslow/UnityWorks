using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GreenAnimationListener : MonoBehaviour
{
    private Animator anim;
    private void Start()
    {
        anim = GetComponent<Animator>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        anim.SetBool("Touched", true);
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        anim.SetBool("Touched", false);
    }
}
