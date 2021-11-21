using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChangeColliderListener : MonoBehaviour
{
    public Collider2D LeftRespawn;
    public Collider2D Stoper;
    public Collider2D RightRespawn;
    public ChangeBallManager BallManager;
    private Sprite[] arrSprite;
    
    void Start()
    {
        arrSprite = BallManager.arrSprite;
        if (PlayerPrefs.HasKey("Element"))
        {
            BallManager.Ball.GetComponent<Image>().sprite = arrSprite[PlayerPrefs.GetInt("Element")];
            BallManager.currentElement = PlayerPrefs.GetInt("Element");
        }       
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {       
        if (gameObject == RightRespawn.gameObject)
        {
            if (BallManager.direction)
            {
                BallManager.selfposition.x = LeftRespawn.gameObject.transform.position.x;
                BallManager.transformTrigger = true;
                BallManager.currentElement++;
                PlayerPrefs.SetInt("Element", BallManager.currentElement);
                if (BallManager.currentElement >= arrSprite.Length)
                {
                    BallManager.currentElement = 0;
                    PlayerPrefs.SetInt("Element", BallManager.currentElement);
                }
                Debug.Log(BallManager.currentElement);
                BallManager.Ball.GetComponent<Image>().sprite = arrSprite[BallManager.currentElement];
            }              
        }
        if (gameObject == LeftRespawn.gameObject)
        {
            if (!BallManager.direction)
            {
                BallManager.selfposition.x = RightRespawn.gameObject.transform.position.x;
                BallManager.transformTrigger = true;
                BallManager.currentElement--;
                PlayerPrefs.SetInt("Element", BallManager.currentElement);
                if (BallManager.currentElement == -1)
                {
                    BallManager.currentElement = arrSprite.Length - 1;
                    PlayerPrefs.SetInt("Element", BallManager.currentElement);
                }
                Debug.Log(BallManager.currentElement);
                BallManager.Ball.GetComponent<Image>().sprite = arrSprite[BallManager.currentElement];
                
            }
        }
        if (gameObject == Stoper.gameObject)
        {
            if (BallManager.transformTrigger)
            {
                BallManager.transformTrigger = false;
                BallManager.moving = false;
                BallManager.anim.SetBool("BackRotation", false);
                BallManager.anim.SetBool("FowardRotation", false);
                BallManager.left.interactable = true;
                BallManager.right.interactable = true;
            }         
        }
    }
}
