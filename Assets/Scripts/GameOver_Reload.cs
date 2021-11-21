using UnityEngine;

public class GameOver_Reload : MonoBehaviour
{
    private Health health;
    private bool updateStoper;
    private LevelTransition levelTransition;
    private void Awake()
    {
        health = GetComponent<Health>();
        levelTransition = GameObject.Find("Screen_transition").GetComponent<LevelTransition>();

    }
    private void Update()
    {
        if (!updateStoper)
        {
            if (!health.isAlive)
            {
                updateStoper = true;
                levelTransition.TransitionStateChanger(3);
            }
        }
    }
}
