using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private GameObject WinWindow;
    [SerializeField] private GameObject FailWindow;
    [SerializeField] private Text CoinsUI;
    [SerializeField] private Text LevelUI;
    [SerializeField] private Text UpgradeUI;
    private SceneController sceneController;
    private void Start()
    {
        sceneController = FindObjectOfType<SceneController>();
        CoinsUI.text = $"{ PlayerPrefs.GetInt("Coins") }";
        if (sceneController.GetCurrentScene() != 0)
        {
            LevelUI.text = $"LEVEL: { sceneController.GetCurrentScene() }";
        }
        else
        {
            UpgradeUI.text = $"COST: { PlayerPrefs.GetInt("UpgradeCost") }";
        }
    }
    public void ShowWinWindow()
    {
        WinWindow.SetActive(true);
    }
    public void ShowFailWindow()
    {
        FailWindow.SetActive(true);
    }
    public void UpdateCoinUI()
    {
        CoinsUI.text = $"{ PlayerPrefs.GetInt("Coins") }";
    }
    public void UpdateUpgradeCostUI()
    {
        UpgradeUI.text = $"COST: { PlayerPrefs.GetInt("UpgradeCost") }";
    }
}
