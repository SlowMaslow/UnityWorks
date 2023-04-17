using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LevelTrigger : MonoBehaviour
{
    private bool triggerCheck;
    private Text PressText;
    public int level;
    private MobileButtonsList mobileButtonList;
    void Start()
    {
        PressText = GameObject.Find("InfoInteractionText").GetComponent<Text>();
        mobileButtonList = FindObjectOfType<MobileButtonsList>();
    }

    // Update is called once per frame
    void Update()
    {
        if (triggerCheck)
        {
            PressText.gameObject.SetActive(true);
            PressText.text = "Press <E> to Enter";
            if (Input.GetKeyDown(KeyCode.E) || mobileButtonList.InteractiveButton.GetComponent<CheckButtonScript>().isRealized)
            {
                triggerCheck = false;
                mobileButtonList.InteractiveButton.GetComponent<CheckButtonScript>().isRealized = false;
                SceneManager.LoadScene(level);
            }
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        triggerCheck = true;
    }
    private void OnTriggerExit(Collider other)
    {
        triggerCheck = false;
        PressText.gameObject.SetActive(false);
    }
}
