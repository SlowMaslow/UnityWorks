using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class PauseScript : MonoBehaviour
{
    private bool trigger;

    public void StopGame()
    {
        Time.timeScale = 0;       
    }
    public void StartGame()
    {
        Time.timeScale = 1;
    }
    public void PauseActiveClick()
    {
        if (trigger == false)
        {
            trigger = true;
            GetComponent<Animator>().SetBool("active", true);
        }
        else
        {
            trigger = false;
            GetComponent<Animator>().SetBool("active", false);
        }
    }
}