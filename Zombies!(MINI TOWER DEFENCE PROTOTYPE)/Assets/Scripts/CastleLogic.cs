using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class CastleLogic : MonoBehaviour
{
    public int health;
    public int StartHealth;
    public Sprite middle;
    public Sprite low;
    public Sprite full;
    public bool death;
    void Update()
    {
        if (!death)
        {
            if (health <= 30 && health > 20)
            {
                GetComponent<Image>().sprite = full;
            }
            if (health <= 20 && health > 10)
            {
                GetComponent<Image>().sprite = middle;
            }
            if (health <= 10 && health > 0)
            {
                GetComponent<Image>().sprite = low;
            }
            if (health <= 0)
            {
                death = true;              
            }
        }
        else
        {
            
        }
            
    }
}
