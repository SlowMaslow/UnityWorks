using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public bool Paused = false;

    public void PausedChanger()
    {
        Paused = !Paused;
    }
}
