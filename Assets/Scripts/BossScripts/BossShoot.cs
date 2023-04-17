using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossShoot : MonoBehaviour
{
    //[SerializeField] private GameObject bullet;
   [SerializeField] private Transform FirePoint;
    public void Shoot(GameObject bullet)
    {
        GameObject currentbullet = Instantiate(bullet, FirePoint.transform.position, GetComponent<Transform>().rotation);
    }
}
