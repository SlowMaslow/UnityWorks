using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

public class LoadRecords : MonoBehaviour
{
    [SerializeField] private string level;
    [SerializeField] private string parameter;
    [SerializeField] private Text TimeText;
    [SerializeField] private Sprite[] images;
    [SerializeField] private Image Bronze;
    [SerializeField] private Image Silver;
    [SerializeField] private Image Gold;
    void Start()
    {
        LoadResults(level, parameter);
        LoadMedals(level);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void LoadResults(string name,string parameter)
    {
        if (PlayerPrefs.HasKey($"{name} {parameter}"))
        {
            TimeText.text = $"{Math.Round(PlayerPrefs.GetFloat($"{name} {parameter}"), 2)} sec";
        }
        else
        {
            TimeText.text = "-";
        }
    }
    private void LoadMedals(string name)
    {
        if (TimeText.text == "-")
        {
            Bronze.sprite = images[0];
            Silver.sprite = images[0];
            Gold.sprite = images[0];
        }
        else
        {
            if (PlayerPrefs.GetFloat($"{name} time") < PlayerPrefs.GetFloat($"{name} bronze"))
            {
                Bronze.sprite = images[1];
                Silver.sprite = images[0];
                Gold.sprite = images[0];
                if (PlayerPrefs.GetFloat($"{name} time") < PlayerPrefs.GetFloat($"{name} silver"))
                {
                    Bronze.sprite = images[1];
                    Silver.sprite = images[2];
                    Gold.sprite = images[0];
                    if (PlayerPrefs.GetFloat($"{name} time") < PlayerPrefs.GetFloat($"{name} gold"))
                    {
                        Bronze.sprite = images[1];
                        Silver.sprite = images[2];
                        Gold.sprite = images[3];
                    }
                }
            }
           
        }
    } 
}
