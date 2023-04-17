using UnityEngine;

public class MobileCheck : MonoBehaviour
{
    [HideInInspector] public int isMobile;
    [SerializeField] private GameObject mobileUI;
    private void Awake()
    { 
        isMobile = PlayerPrefs.GetInt("isMobile");
    }
    private void Start()
    {
        if(isMobile == 0)
        {
            mobileUI.SetActive(false);
        }
    }
}
