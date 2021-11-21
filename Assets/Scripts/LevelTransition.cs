using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransition : MonoBehaviour
{
    public void FirstLevelTransition()
    {
        SceneManager.LoadScene(1);
        PlayerPrefs.DeleteKey("Level");
    }
    public void ReloadLevelTransition()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void ContinueTransition()
    {
        SceneManager.LoadScene(PlayerPrefs.GetInt("Level"));
    }

    public void MainMenuTransition()
    {
        SceneManager.LoadScene(0);
    }

    public void TransitionStateChanger(int StateNum)
    {
        GameObject.Find("Screen_transition").GetComponent<Animator>().SetInteger("TransitionState", StateNum);
    }
    public void NextLevelTransition()
    {
        //Debug.Log(SceneManager.sceneCountInBuildSettings);
        if (SceneManager.GetActiveScene().buildIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
        else
        {
            MainMenuTransition();
        }
    }

    public void TimeScaleing(float num)
    {
        Time.timeScale = num;
    }
}
