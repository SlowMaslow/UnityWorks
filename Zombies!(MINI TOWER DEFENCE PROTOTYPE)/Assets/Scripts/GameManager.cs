using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class GameManager : MonoBehaviour
{
    private ObjectListener objectManager;
    private GameObject[] Destroyer;
    //----------------------------------
    public Image DayNight;
    public Image SunMoon;
    //----------------------------------
    public int ResourceFromFarmer;
    public int ResourceNow;
    public int CostFarmer;
    public int CostWar;
    public int NeedWarResourse;
    public int WaveZombieCount;
    //----------------------------------
    public float CoolDownDayTime;
    public float CoolDownZombieSpawn;
    public float CoolDownEating;
    public float CoolDownWarrior;
    public float CoolDownFarmer;
    //----------------------------------
    private float currentTime;
    private float currentTimeEating;
    private float currentZombieSpawnTime;
    private float currentWarTime;
    private float currentFarmerTime;
    //----------------------------------
    private float r;
    private float g;
    private float b;
    private float t;
    //----------------------------------
    private bool tickEating;
    private bool tickDay;
    private bool tickRespawnZombie;
    private bool tickWarrior;
    private bool tickFarmer;
    //----------------------------------
    private int startResource;
    private int startZombieCount;
    private int farmerCount;
    private int dayGones;
    private int waveZombieCounter;
    private int dayCounter;
    private float rotationDay;
    //---------------------------------
    public bool stop;
    void Start()
    {
        objectManager = GameObject.Find("GameObjectManager").GetComponent<ObjectListener>();
        currentTime = CoolDownDayTime;
        currentZombieSpawnTime = CoolDownZombieSpawn;
        currentTimeEating = CoolDownEating;
        currentWarTime = CoolDownWarrior;
        currentFarmerTime = CoolDownFarmer;
        objectManager.Castle.GetComponent<CastleLogic>().StartHealth = objectManager.Castle.GetComponent<CastleLogic>().health;
        stop = true;
        saveNewGameState();
        stopGame();
    }
    void Update()
    {
        if (!stop)
        {
            textUpdate();
            activeButtonListener();
            dayListener();
            zombieSpawnListener();
            eatingListener();
            farmerListener();
            warriorListener();  
        }
        if (objectManager.Castle.GetComponent<CastleLogic>().health <= 0)
        {
            GameOver();
        }
    }
    private void activeButtonListener()
    {
        if (ResourceNow < CostFarmer)
        {
            objectManager.ButtonCreateFarmer.interactable = false;
        }
        else
        {
            objectManager.ButtonCreateFarmer.interactable = true;
        }
        if (ResourceNow < CostWar)
        {
            objectManager.ButtonCreateWarrior.interactable = false;
        }
        else
        {
            objectManager.ButtonCreateWarrior.interactable = true;
        }
    }
    private void dayListener()
    {
        tickDay = false;
        currentTime -= Time.deltaTime;
        dayAnimation();
        if (currentTime <= 0)
        {
            tickDay = true;
            currentTime = CoolDownDayTime;
            dayCounter++;
            t = 0;
            if ((dayCounter + 2) % 2 != 0)
            {
                GetComponent<AudioSource>().Play();
                dayGones++;
                waveZombieCounter = WaveZombieCount;
                WaveZombieCount = Convert.ToInt32(Mathf.Floor(WaveZombieCount * 1.6f));
            }
        }
    }
    private void zombieSpawnListener()
    {
        tickRespawnZombie = false;
        currentZombieSpawnTime -= Time.deltaTime;
        if (currentZombieSpawnTime <= 0)
        {
            tickRespawnZombie = true;
            currentZombieSpawnTime = CoolDownZombieSpawn;
        }
        if (tickRespawnZombie == true)
        {
            if (waveZombieCounter != 0)
            {
                gameObject.GetComponent<BotsCreater>().BotCreate(objectManager.Zombie, objectManager.RespawnZombie, objectManager.CanvasGame);
                waveZombieCounter--;
            }
        }
    }
    private void eatingListener()
    {
        tickEating = false;
        currentTimeEating -= Time.deltaTime;
        if (currentTimeEating <= 0)
        {
            tickEating = true;
            currentTimeEating = CoolDownEating;
        }
        if (tickEating == true)
        {
            ResourceNow = ResourceNow - (GameObject.FindGameObjectsWithTag("Good").Length) * NeedWarResourse + (farmerCount * ResourceFromFarmer);
            if (ResourceNow < 0)
            {
                ResourceNow = 0;
            }
        }
    }
    private void farmerListener()
    {
        if (tickFarmer)
        {
            currentFarmerTime -= Time.deltaTime;
            objectManager.ButtonCreateFarmer.GetComponent<Image>().fillAmount = 1 - (currentFarmerTime / CoolDownFarmer);
            objectManager.ButtonCreateFarmer.interactable = false;
            if (currentFarmerTime <= 0)
            {
                currentFarmerTime = CoolDownFarmer;
                tickFarmer = false;
                objectManager.ButtonCreateFarmer.interactable = true;
            }
        }
    }
    private void warriorListener()
    {
        if (tickWarrior)
        {
            currentWarTime -= Time.deltaTime;
            objectManager.ButtonCreateWarrior.interactable = false;
            objectManager.ButtonCreateWarrior.GetComponent<Image>().fillAmount = 1 - (currentWarTime / CoolDownWarrior);
            if (currentWarTime <= 0)
            {
                currentWarTime = CoolDownWarrior;
                tickWarrior = false;
                objectManager.ButtonCreateWarrior.interactable = true;
            }
        }
    }
    private void textUpdate()
    {
        objectManager.TextWarrior.text = GameObject.FindGameObjectsWithTag("Good").Length.ToString();
        objectManager.TextNextWave.text = WaveZombieCount.ToString();
        objectManager.TextWavesGone.text = dayGones.ToString();
        objectManager.TextEatResource.text = (GameObject.FindGameObjectsWithTag("Good").Length * NeedWarResourse).ToString();
        objectManager.TextResource.text = ResourceNow.ToString();
        objectManager.TextIncomeResource.text = (farmerCount * ResourceFromFarmer).ToString();
        objectManager.TextFarmer.text = farmerCount.ToString();
    }
    private void saveNewGameState()
    {
        startResource = ResourceNow;
        startZombieCount = WaveZombieCount;
    }
    public void onFarmerClick()
    {
        farmerCount++;
        ResourceNow -= CostFarmer;
        tickFarmer = true;
    }
    public void onWarClick()
    {
        ResourceNow -= CostWar;
        gameObject.GetComponent<BotsCreater>().BotCreate(objectManager.Warrior, objectManager.RespawnWarrior, objectManager.CanvasGame);
        tickWarrior = true;   
    }
    public void onHomeClick()
    {
        DestroyObjects();
        stop = true;        
    }
    private void DestroyObjects()
    {
        Destroyer = GameObject.FindGameObjectsWithTag("Good");
        foreach (GameObject destroy in Destroyer)
        {
            Destroy(destroy);
        }
        Destroyer = GameObject.FindGameObjectsWithTag("Bad");
        foreach (GameObject destroy in Destroyer)
        {
            Destroy(destroy);
        }
    }
    public void stopGame()
    {
        Time.timeScale = 0;
    }
    public void playGame()
    {
        Time.timeScale = 1;
    }
    public void GameOver()
    {
        objectManager.GameOverPanel.SetActive(true);
        objectManager.GameOverPanel.GetComponent<AudioSource>().Play();
        stop = true;
        stopGame();
    }
    public void StartGame()
    {
        objectManager.CanvasMainMenu.GetComponent<AudioSource>().Stop();
        objectManager.CanvasGame.GetComponent<AudioSource>().Play();
        RestartGame();
        stop = false;
    }
    private void RestartGame()
    {
        DestroyObjects();
        dayGones = 0;
        t = 0;
        farmerCount = 0;
        dayCounter = 0;
        waveZombieCounter = 0;
        ResourceNow = startResource;
        WaveZombieCount = startZombieCount;
        currentTime = CoolDownDayTime;
        currentTimeEating = CoolDownEating;
        currentFarmerTime = CoolDownFarmer;
        currentWarTime = CoolDownWarrior;
        objectManager.GameOverPanel.SetActive(false);
        objectManager.Castle.GetComponent<CastleLogic>().death = false;
        objectManager.Castle.GetComponent<CastleLogic>().health = objectManager.Castle.GetComponent<CastleLogic>().StartHealth;
        playGame();
    }
    private void dayAnimation()
    {
        if ((dayCounter + 2) % 2 == 0)
        {
            r = Mathf.Lerp(255, 0, t);
            g = Mathf.Lerp(255, 0, t);
            b = Mathf.Lerp(255, 0, t);
            rotationDay = Mathf.Lerp(0, -180, t);
            t = 1 - (currentTime / CoolDownDayTime);
            DayNight.color = new Color32(Convert.ToByte(Mathf.Abs(r)), Convert.ToByte(Mathf.Abs(g)), Convert.ToByte(Mathf.Abs(b)), 255);
            SunMoon.GetComponent<Transform>().rotation = Quaternion.Euler(0, 0, Mathf.Abs(rotationDay));
        }
        else
        {
            r = Mathf.Lerp(0, 255, t);
            g = Mathf.Lerp(0, 255, t);
            b = Mathf.Lerp(0, 255, t);
            rotationDay = Mathf.Lerp(-180, -360, t);
            t = 1 - (currentTime / CoolDownDayTime);
            DayNight.color = new Color32(Convert.ToByte(Mathf.Abs(r)), Convert.ToByte(Mathf.Abs(g)), Convert.ToByte(Mathf.Abs(b)), 255);
            SunMoon.GetComponent<Transform>().rotation = Quaternion.Euler(0, 0, Mathf.Abs(rotationDay));
        }
    }
}
