using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeController : MonoBehaviour
{
    private UIController uiController;
    private PlayerStats playerStats;
    void Start()
    {
        playerStats = FindObjectOfType<PlayerStats>();
        uiController = FindObjectOfType<UIController>();
        firstPlayCheck();
    }

    private void firstPlayCheck()
    {
        if (!PlayerPrefs.HasKey("FirstPlay"))
        {
            PlayerPrefs.SetInt("FirstPlay", 1);
            PlayerPrefs.SetInt("UpgradeCost", 1);
            uiController.UpdateUpgradeCostUI();
        }
        else
        {
            uiController.UpdateUpgradeCostUI();
        }
    }

    public void Upgrading()
    {
        if(PlayerPrefs.GetInt("UpgradeCost") <= PlayerPrefs.GetInt("Coins"))
        {
            PlayerPrefs.SetInt("Coins", PlayerPrefs.GetInt("Coins") - PlayerPrefs.GetInt("UpgradeCost"));
            playerStats.Coins = PlayerPrefs.GetInt("Coins");
            PlayerPrefs.SetInt("UpgradeCost", PlayerPrefs.GetInt("UpgradeCost") + 1);
            uiController.UpdateUpgradeCostUI();
            uiController.UpdateCoinUI();
        }
    }
}
