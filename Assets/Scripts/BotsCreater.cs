using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotsCreater : MonoBehaviour
{
    private GameObject realizer;
    public void BotCreate(GameObject Prefab, GameObject Respawn, GameObject GamePlace)
    {
        realizer = Instantiate(Prefab, Respawn.transform.position, Quaternion.identity);
        realizer.transform.SetParent(GamePlace.transform, false);
        realizer.transform.position = Respawn.transform.position;
    }
}
