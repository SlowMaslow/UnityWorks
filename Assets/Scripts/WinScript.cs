using UnityEngine;
using UnityEngine.SceneManagement;

public class WinScript : MonoBehaviour
{
    private TimeManager timeManager;
    private void Start()
    {
        timeManager = FindObjectOfType<TimeManager>();
    }
    public void SaveResults()
    {
        if (PlayerPrefs.HasKey($"{SceneManager.GetActiveScene().name} time"))
        {
            if (PlayerPrefs.GetFloat($"{SceneManager.GetActiveScene().name} time") > timeManager.currentTime)
            {
                PlayerPrefs.SetFloat($"{SceneManager.GetActiveScene().name} time", timeManager.currentTime);
            }
        }
        else
        {
            PlayerPrefs.SetFloat($"{SceneManager.GetActiveScene().name} time", timeManager.currentTime);
        }
        PlayerPrefs.SetInt("LastLevel", SceneManager.GetActiveScene().buildIndex);
    }

}
