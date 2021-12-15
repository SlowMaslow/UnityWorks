using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WinScript : MonoBehaviour
{
    public int WinValue;
    private bool isWin;
    [SerializeField] private GameObject WinWindow;

    private void Start()
    {
        isWin = false;
    }

    void Update()
    {
        if (!isWin)
        {
            if (WinValue == 2)
            {
                WinWindowActive();
            }
        }
    }

    private void WinWindowActive()
    {
        PlayerPrefs.SetInt("LastLevel", PlayerPrefs.GetInt("LastLevel") + 1);
        WinWindow.SetActive(true);
        isWin = true;
    }
}
