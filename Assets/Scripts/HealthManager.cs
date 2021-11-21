using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class HealthManager : MonoBehaviour
{
    [SerializeField] public int health;
    private int maxHealth;
    public bool immortal;
    private float immortalcooldown = 1f;
    private float currentimmortalcooldown;
    [SerializeField] private Image[] healthImage;
    [SerializeField] private Sprite[] healthSprite;

    private void Start()
    {
        maxHealth = health;
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().buildIndex != 1)
        {
            for (int i = 0; i < maxHealth; i++)
            {
                if (i < health)
                    healthImage[i].sprite = healthSprite[0];
                else
                    healthImage[i].sprite = healthSprite[1];
            }
            if (immortal)
            {
                if (currentimmortalcooldown < immortalcooldown)
                    currentimmortalcooldown += Time.deltaTime;
                else
                {
                    immortal = false;
                    currentimmortalcooldown = 0;
                }
            }
            if (health <= 0)
            {
                gameObject.transform.position = GameObject.Find("StartPosition").transform.position;
                health = maxHealth;
            }
        } 
    }
}
