using UnityEngine;

public class FirstRun : MonoBehaviour
{
    public GameObject tutorial;
     void Start()
    {
        if (!PlayerPrefs.HasKey("FirstRun"))
        {
            PausedChanger();
            Instantiate(tutorial, FindObjectOfType<Canvas>().transform);
            PlayerPrefs.SetInt("FirstRun", 1);
        }
    }
    private void PausedChanger()
    {
        FindObjectOfType<TimeManager>().PausedChanger();
    }
}
