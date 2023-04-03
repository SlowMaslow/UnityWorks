using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour
{
    [SerializeField] private Text UITimer;
    [HideInInspector] public bool Paused;
    [HideInInspector] public float currentTime;
    [HideInInspector] public bool WinTrigger;
    private void Awake()
    {
        Paused = false;
    }
    void FixedUpdate()
    {
        if (!WinTrigger && !Paused)
        {
            currentTime += Time.deltaTime;
            UITimer.text = $"{currentTime.ToString("0.00")} sec";
        } 
    }
    public void PausedChanger()
    {
        Paused = !Paused;
    }
}
