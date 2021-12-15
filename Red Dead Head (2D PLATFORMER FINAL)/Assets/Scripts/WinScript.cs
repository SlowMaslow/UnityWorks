using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WinScript : MonoBehaviour
{
    private LevelTransition levelTransition;
    private void Awake()
    {
        levelTransition = GameObject.Find("Screen_transition").GetComponent<LevelTransition>();
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {        
        if (collision.gameObject.name.Equals("Main_Hero"))
        {
            levelTransition.TransitionStateChanger(4);
            if (SceneManager.GetActiveScene().buildIndex + 2 < SceneManager.sceneCountInBuildSettings)
            {
                PlayerPrefs.SetInt("Level", SceneManager.GetActiveScene().buildIndex + 1);
            }
        }
    }
}
