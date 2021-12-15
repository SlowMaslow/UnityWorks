using UnityEngine;
using UnityEngine.UI;
public class UI_HealthManager : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Text currentHealth;
    private Health health;
    void Start()
    {
        health = GetComponent<Health>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void UI_SetMaxHealth(float SlideBarMaxValue)
    {
        slider.maxValue = SlideBarMaxValue;
        slider.value = slider.maxValue;
    }

    public void UI_HealthChanger(float UI_ValueForChange)
    {
        slider.value -= UI_ValueForChange;
        currentHealth.text = slider.value.ToString();
        if (slider.value < 0)
        {
            slider.value = 0;
            currentHealth.text = slider.value.ToString();
        } 
    }
}
