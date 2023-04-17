using UnityEngine;
using UnityEngine.UI;

public class PortativeModeChanger : MonoBehaviour
{
    void Start()
    {
        if (PlayerPrefs.HasKey("isMobile"))
        {
            if (PlayerPrefs.GetInt("isMobile") == 1)
            {
                GetComponent<Toggle>().isOn = true;
            }
            else
            {
                GetComponent<Toggle>().isOn = false;
            }
        }
        else
        {
            PlayerPrefs.SetInt("isMobile", 0);
            GetComponent<Toggle>().isOn = false;
        }
    }

    public void MobileModeChange()
    {
        if (GetComponent<Toggle>().isOn == false)
        {
            PlayerPrefs.SetInt("isMobile", 0);
        }
        else
        {
            PlayerPrefs.SetInt("isMobile", 1);
        }
    }
}
