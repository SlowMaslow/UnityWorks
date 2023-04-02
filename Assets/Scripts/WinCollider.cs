using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WinCollider : MonoBehaviour
{
    private EndLevelScript endLevelScript;
    private TimeManager timeManager;
    private WinScript winScript;
    private void Start()
    {
        endLevelScript = GetComponent<EndLevelScript>();
        timeManager = FindObjectOfType<TimeManager>();
        winScript = FindObjectOfType<WinScript>();
    }
    private void OnTriggerEnter(Collider other)
    {
        endLevelScript.EndLevel();
        PlayerPrefs.SetInt("LastLevel", PlayerPrefs.GetInt("LastLevel") + 1);
        winScript.SaveResults();
    }
}
