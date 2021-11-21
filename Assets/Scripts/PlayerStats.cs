using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public int Coins;

    void Start()
    {
        Coins = PlayerPrefs.GetInt("Coins");
    }
}
