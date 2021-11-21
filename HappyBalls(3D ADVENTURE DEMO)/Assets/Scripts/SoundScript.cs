using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SoundScript : MonoBehaviour
{
    public Sprite[] arrSprite;
    public AudioMixer mixer;
    private Image selfImage;
    private bool trigger;

    void Start()
    {
        selfImage = gameObject.GetComponent<Image>();
        if (!PlayerPrefs.HasKey("Soundtrigger"))
        {
            PlayerPrefs.SetInt("Soundtrigger", 1);
        }
        if (!PlayerPrefs.HasKey("Musictrigger"))
        {
            PlayerPrefs.SetInt("Musictrigger", 1);
        }
        if (mixer.name == "Sounds")
        {  
            if (PlayerPrefs.GetInt("Soundtrigger") == 1)
            {
                selfImage.sprite = arrSprite[0];
                mixer.SetFloat("Volume", 0);
                trigger = true;
            }
            else
            {
                selfImage.sprite = arrSprite[1];
                mixer.SetFloat("Volume", -80f);
                trigger = false;
            }
        }
        if (mixer.name == "Music")
        {
            if (PlayerPrefs.GetInt("Musictrigger") == 1)
            {
                selfImage.sprite = arrSprite[0];
                mixer.SetFloat("Volume", 0);
                trigger = true;
            }
            else
            {
                selfImage.sprite = arrSprite[1];
                mixer.SetFloat("Volume", -80f);
                trigger = false;
            }
        }
    }

    public void SpriteChanger()
    {
        if (trigger)
        {
            selfImage.sprite = arrSprite[1];
            mixer.SetFloat("Volume", -80f);
            trigger = false;
            if (mixer.name == "Sounds")
            {
                PlayerPrefs.SetInt("Soundtrigger", 0);
            }
            if (mixer.name == "Music")
            {
                PlayerPrefs.SetInt("Musictrigger", 0);
            }
        }
        else
        {
            selfImage.sprite = arrSprite[0];
            mixer.SetFloat("Volume", 0);
            trigger = true;
            if (mixer.name == "Sounds")
            {
                PlayerPrefs.SetInt("Soundtrigger", 1);
            }
            if (mixer.name == "Music")
            {
                PlayerPrefs.SetInt("Musictrigger", 1);
            }
        }
    }
}