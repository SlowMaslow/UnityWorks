using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossStateMachine : MonoBehaviour
{
    [SerializeField] private Animator animBoss;
    public void toAttack()
    {
        animBoss.SetInteger("State", 1);
    }
    public void toIdle()
    {
        animBoss.SetInteger("State", 0);
    }

    public void StopBoss()
    {
        transform.parent.GetComponent<Animator>().enabled = false;
    }
}
