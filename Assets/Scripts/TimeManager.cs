using System;
using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
    private Text UITimer;
    [HideInInspector] public float currentTime;
    [HideInInspector] public bool WinTrigger;

    void Start()
    {
        UITimer = GetComponent<Text>();
    }


    void FixedUpdate()
    {
        if (!WinTrigger)
        {
            currentTime += Time.deltaTime;
            UITimer.text = Math.Round(currentTime, 2).ToString();
        } 
    }
}
