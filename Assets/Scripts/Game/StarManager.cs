using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class StarManager : MonoBehaviour
{
    [SerializeField] public int stars;
    private int maxStars;
    private bool win;
    [SerializeField] private Image[] starImage;
    [SerializeField] private Sprite[] starSprite;
    [SerializeField] private Animator anim;
    [SerializeField] private AudioSource winsound;

    private void Start()
    {
        maxStars = stars;
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().buildIndex != 1)
        {
            for (int i = 0; i < maxStars; i++)
            {
                if (i < stars)
                    starImage[i].sprite = starSprite[0];
                else
                {
                    starImage[i].sprite = starSprite[1];
                }     
            }
            if (stars == 0 && !win)
            {
                win = true;
                anim.SetBool("active", true);
                winsound.Play();
            }
        }
    }
}
