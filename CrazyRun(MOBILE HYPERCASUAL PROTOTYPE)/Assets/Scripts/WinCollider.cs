using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WinCollider : MonoBehaviour
{
    private EndLevelScript endLevelScript;
    private TimeManager timeManager;
    private void Start()
    {
        endLevelScript = GetComponent<EndLevelScript>();
        timeManager = FindObjectOfType<TimeManager>();
    }
    private void OnTriggerEnter(Collider other)
    {
        endLevelScript.EndLevel();
        timeManager.WinTrigger = true;
        PlayerPrefs.SetInt("LastLevel", PlayerPrefs.GetInt("LastLevel") + 1);
    }
}
