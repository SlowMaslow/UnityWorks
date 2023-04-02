using System;
using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
    private Text UITimer;
    private string cache;
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
            cache = currentTime.ToString("F2");
            UITimer.text = $"{cache} sec";
        } 
    }
}
