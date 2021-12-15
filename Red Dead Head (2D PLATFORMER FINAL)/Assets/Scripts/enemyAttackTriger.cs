using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class enemyAttackTriger : MonoBehaviour
{
    [HideInInspector] public bool isAttack;
    private void Start()
    {
        isAttack = false;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.name.Equals("Main_Hero"))
        {
            isAttack = true;
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.name.Equals("Main_Hero"))
        {
            isAttack = false;
        }
    }
}
