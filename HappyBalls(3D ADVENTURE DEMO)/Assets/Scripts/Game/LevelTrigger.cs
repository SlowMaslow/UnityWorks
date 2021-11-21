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
    void Start()
    {
        PressText = GameObject.Find("Press").GetComponent<Text>();
    }

    // Update is called once per frame
    void Update()
    {
        if (triggerCheck)
        {
            PressText.gameObject.SetActive(true);
            PressText.text = "Press <E> to Enter";
            if (Input.GetKeyDown(KeyCode.E))
            {
                triggerCheck = false;
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
