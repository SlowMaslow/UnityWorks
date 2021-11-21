using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarCollect : MonoBehaviour
{
    [SerializeField] private Animator anim;
    [SerializeField] private AudioSource starsound;
    private StarManager starManager;
    private bool collect;
    private void Start()
    {
        starManager = GameObject.FindObjectOfType<StarManager>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!collect)
        {
            anim.SetBool("collect", true);
            starManager.stars = starManager.stars - 1;
            collect = true;
            starsound.Play();
        }    
    }
    public void DestroyStar()
    {
        Destroy(transform.parent.gameObject);
    }

}
