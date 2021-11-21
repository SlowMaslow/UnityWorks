using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChangeBallManager : MonoBehaviour
{
    public float speed;
    public bool direction;
    public bool moving;
    public Button right;
    public Button left;
    public GameObject Ball;
    public Vector3 selfposition;
    public Animator anim;
    public bool transformTrigger;
    public Sprite[] arrSprite;
    public int currentElement;

    void Start()
    {
        selfposition = Ball.GetComponent<RectTransform>().position;
        anim = Ball.GetComponent<Animator>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (moving)
        {
            selfposition.x += speed;
            Ball.transform.position = selfposition;
        }       
    }
    public void LeftClick()
    {
        direction = false;
        moving = true;
        speed = -20f;
        anim.SetBool("BackRotation", true);
        anim.SetBool("FowardRotation", false);
        left.interactable = false;
        right.interactable = false;
    }
    public void RightClick()
    {
        direction = true;
        moving = true;
        speed = 20f;
        anim.SetBool("FowardRotation", true);
        anim.SetBool("BackRotation", false);
        left.interactable = false;
        right.interactable = false;
    }
}
