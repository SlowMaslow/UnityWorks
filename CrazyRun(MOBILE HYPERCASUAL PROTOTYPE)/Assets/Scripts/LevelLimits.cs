using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelLimits : MonoBehaviour
{
    public float Gold;
    public float Silver;
    public float Bronze;
    [SerializeField] private EndLevelScript endLevelScript;
    private TimeManager timeManager;

    void Start()
    {
        GameObject.Find("bronzeTime").GetComponent<Text>().text = $"{Bronze.ToString()} sec";
        GameObject.Find("silverTime").GetComponent<Text>().text = $"{Silver.ToString()} sec";
        GameObject.Find("goldTime").GetComponent<Text>().text = $"{Gold.ToString()} sec";
        timeManager = FindObjectOfType<TimeManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (timeManager.currentTime > Bronze && !endLevelScript.Fail)
        {
            endLevelScript.EndLevel();
            endLevelScript.Fail = true;
        }
    }
}
