using UnityEngine;

public class WinScript : MonoBehaviour
{
    public int WinValue;
    private bool isWin;
    private TimeManager timeManager;
    private GameObject pausePrefab;
    [SerializeField] private GameObject WinWindow;

    private void Start()
    {
        timeManager = FindObjectOfType<TimeManager>();
        pausePrefab = GameObject.Find("Pause");
        isWin = false;
    }

    void Update()
    {
        if (!isWin)
        {
            if (WinValue == 2)
            {
                pausePrefab.SetActive(false);
                timeManager.PausedChanger();
                WinWindowActive();
            }
        }
    }

    private void WinWindowActive()
    {
        WinWindow.SetActive(true);
        isWin = true;
    }
}
