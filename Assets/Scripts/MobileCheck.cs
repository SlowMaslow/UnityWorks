using UnityEngine;

public class MobileCheck : MonoBehaviour
{
    [HideInInspector] public int isMobile;
    private void Awake()
    { 
        isMobile = PlayerPrefs.GetInt("isMobile");
    }
}
