using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SummonRespawnScript : MonoBehaviour
{
    [SerializeField] private GameObject summon;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SummonRespawn(string respawn)
    {
        GameObject currentSummon = Instantiate(summon, GameObject.Find(respawn).transform.position, GameObject.Find(respawn).transform.rotation);
    }
}
