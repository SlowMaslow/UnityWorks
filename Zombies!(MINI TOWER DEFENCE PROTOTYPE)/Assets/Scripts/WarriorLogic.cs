using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class WarriorLogic : MonoBehaviour
{
    private GameObject MoveTarget;
    private GameObject[] arrEnemy;
    private AudioSource slash;
    private System.Random rnd = new System.Random();
    private Vector3 selfposition;
    //---------------------------
    public float direction;
    public float hitCooldown;
    public int health;   
    public float directionSpeed;
    //--------------------------
    private float currentTime;
    private bool fight;
    private bool death;
    private bool inHome;
    private bool clear;
    private bool target;
    private bool hit;
    void Start()
    {
        selfposition = GetComponent<RectTransform>().position;
        slash = GetComponent<AudioSource>();
        GetComponent<Animator>().Play("idle");
    }
    void Update()
    {
        deathListhener();
        if (!death && Time.timeScale != 0)
        {
            targetListener();
            moveListener();
            atackListener();           
        }
    }
    private void atackListener()
    {
        if (fight)
        {
            hit = false;
            currentTime -= Time.deltaTime;
            if (currentTime <= 0)
            {
                hit = true;
                currentTime = hitCooldown;
            }
            if (hit)
            {
                if (!death && MoveTarget.GetComponent<ZombieLogic>().health > 0)
                {
                    slash.Play();
                    GetComponent<Animator>().Play("attack");
                    MoveTarget.GetComponent<ZombieLogic>().health -= 1;
                }
            }
        }       
    }
    private void deathListhener()
    {
        if (health <= 0)
        {
            GetComponent<Animator>().Play("death_01");
            death = true;
        }
    }
    private void moveListener()
    {
        if (target)
        {
            if (!GetComponent<Animation>().IsPlaying("walk"))
            {
                GetComponent<Animator>().Play("walk");
            }
            if (MoveTarget.transform.position.x + MoveTarget.transform.localScale.x / 2 < transform.position.x)
            {
                selfposition.x -= direction;
                transform.position = selfposition;
                transform.eulerAngles = new Vector3(0, 0, 0);
            }
            else
            {
                selfposition.x += direction;
                transform.position = selfposition;
                transform.eulerAngles = new Vector3(0, 180, 0);
            }
        }
    }
    private void targetListener()
    {
        if (GameObject.FindGameObjectWithTag("Bad") != null)
        {
            if (MoveTarget == null || MoveTarget.tag != "Bad")
            {
                arrEnemy = GameObject.FindGameObjectsWithTag("Bad");
                MoveTarget = arrEnemy[rnd.Next(0, arrEnemy.Length)];
                target = true;
                clear = false;
                inHome = false;
            } 
        }
        if (GameObject.FindGameObjectWithTag("Bad") == null)
        {
            clear = true;
            fight = false;
            if (MoveTarget == null && !inHome)
            {
                MoveTarget = GameObject.FindGameObjectWithTag("Castle");
                target = true;
            }
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!death && collision.gameObject == MoveTarget)
        {
            target = false;
            GetComponent<Animator>().Play("idle");
        }          
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.gameObject.tag == "Castle" && !death)
        {
            target = false;
            inHome = true;
            MoveTarget = null;
            GetComponent<RectTransform>().eulerAngles = new Vector3(0, 0, 0);
            GetComponent<Animator>().Play("idle");
        }
        if (collision.gameObject == MoveTarget)
        {
            if (!GetComponent<Animation>().IsPlaying("idle") && !fight)
            {
                GetComponent<Animator>().Play("idle");
            }
            target = false;
        }
        if (!fight && collision.gameObject.tag != "Castle" && collision.gameObject.tag != "Good")
        {
            fight = true;
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject == MoveTarget)
        {
            fight = false;
            target = true;
        }
    }
    public void destroyer()
    {
        Destroy(gameObject);
    }
}
