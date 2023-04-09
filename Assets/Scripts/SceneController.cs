using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public int GetCurrentScene() 
    {
        return SceneManager.GetActiveScene().buildIndex;
    }
    public void LoadNextScene()
    {
        if ((SceneManager.sceneCountInBuildSettings - 1)  > PlayerPrefs.GetInt("LastLevel"))
        {
            SceneManager.LoadScene(GetCurrentScene() + 1);
        }
        else
        {
            Debug.Log(SceneManager.sceneCountInBuildSettings);
            Debug.Log(PlayerPrefs.GetInt("LastLevel"));
            SceneManager.LoadScene(1);
        }
    }
    public void LoadMainMenu()
    {
        SceneManager.LoadScene(0);
    }
    public void LoadCurrentScene()
    {
        SceneManager.LoadScene(GetCurrentScene());
    }
    public void LoadLastLevel()
    {
        if (PlayerPrefs.GetInt("LastLevel") != 0 && SceneManager.sceneCountInBuildSettings > PlayerPrefs.GetInt("LastLevel"))
        {
            SceneManager.LoadScene(PlayerPrefs.GetInt("LastLevel"));
        }
        else
        {
            PlayerPrefs.SetInt("LastLevel", 1);
            SceneManager.LoadScene(1);
        }
    }
    public void SaveLastLevel()
    {
        PlayerPrefs.SetInt("LastLevel", GetCurrentScene());
    }
    public void SaveLastLevelOnNext()
    {
        if (SceneManager.sceneCountInBuildSettings - 1 > PlayerPrefs.GetInt("LastLevel"))
        {
            Debug.Log(SceneManager.sceneCountInBuildSettings);
            Debug.Log(PlayerPrefs.GetInt("LastLevel"));
            PlayerPrefs.SetInt("LastLevel", GetCurrentScene() + 1);
        }
        else
        {
            PlayerPrefs.SetInt("LastLevel", 0);
        }
    }
}
