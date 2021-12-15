using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectListener : MonoBehaviour
{
    public Button ButtonStartMainMenu;
    public Button ButtonMuteSoundMainMenu;
    //---------------------------------------------
    public Button ButtonCreateWarrior;
    public Button ButtonCreateFarmer;
    public Button ButtonPause;
    public Button ButtonPauseClose;
    public Button ButtonMuteSoundGame;
    public Button ButtonToMainMenu;
    public Button ButtonRestartGame;
    public Button ButtonGameOverToMainMenu;
    //---------------------------------------------
    public GameObject CanvasMainMenu;
    public GameObject CanvasGame;
    //---------------------------------------------
    public GameObject Warrior;
    public GameObject Zombie;
    public GameObject Castle;
    public GameObject RespawnWarrior;
    public GameObject RespawnZombie;
    public GameObject GameOverPanel;
    //---------------------------------------------
    public Text TextWarrior;
    public Text TextFarmer;
    public Text TextResource;
    public Text TextNextWave;
    public Text TextEatResource;
    public Text TextIncomeResource;
    public Text TextWavesGone;
}
