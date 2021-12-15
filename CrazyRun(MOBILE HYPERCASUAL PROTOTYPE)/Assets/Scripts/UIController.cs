using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private GameObject WinWindow;
    [SerializeField] private GameObject FailWindow;
    [SerializeField] private Text LevelUI;
    private SceneController sceneController;
    private void Start()
    {
        sceneController = FindObjectOfType<SceneController>();
        LevelUI.text = $"LEVEL: { sceneController.GetCurrentScene() }";
    }
    public void ShowWinWindow()
    {
        WinWindow.SetActive(true);
    }
    public void ShowFailWindow()
    {
        FailWindow.SetActive(true);
    }
}
