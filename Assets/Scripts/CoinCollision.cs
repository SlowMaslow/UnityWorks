using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinCollision : MonoBehaviour
{
    private PlayerStats playerStats;
    private UIController uiController;
    private bool isCollided;
    private void Start()
    {
        uiController = FindObjectOfType<UIController>();
        playerStats = FindObjectOfType<PlayerStats>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!isCollided)
        {
            CollectCoin();
            DestroyCoin();
        }
    }
    public void DestroyCoin()
    {
        Destroy(transform.parent.gameObject);
    }

    private void CollectCoin()
    {
        isCollided = true;
        PlayerPrefs.SetInt("Coins", PlayerPrefs.GetInt("Coins") + 1);
        playerStats.Coins = PlayerPrefs.GetInt("Coins");
        uiController.UpdateCoinUI();
    }
}
