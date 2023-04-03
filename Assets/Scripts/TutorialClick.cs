using UnityEngine;

public class TutorialClick : MonoBehaviour
{
    public void PausedChanger()
    {
        FindObjectOfType<TimeManager>().PausedChanger();
    }
}
