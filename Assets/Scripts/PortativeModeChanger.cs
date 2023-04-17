using UnityEngine;
using UnityEngine.UI;

public class PortativeModeChanger : MonoBehaviour
{
    // Start is called before the first frame update
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
        //Debug.Log(PlayerPrefs.GetInt("isMobile"));
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
