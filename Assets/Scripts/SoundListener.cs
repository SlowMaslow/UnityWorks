using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class SoundListener : MonoBehaviour
{
    private bool switcher;
    public Sprite SwitchOn;
    public Sprite SwitchOff;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void OnSwitch()
    {
        if (!switcher)
        {
            AudioListener.volume = 0;
            gameObject.GetComponent<Image>().sprite = SwitchOff;
            switcher = true;
        }
        else
        {
            AudioListener.volume = 1;
            gameObject.GetComponent<Image>().sprite = SwitchOn;
            switcher = false;
        }
        
    }
}
