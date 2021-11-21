using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIShower : MonoBehaviour
{
    public GameObject UIPanel;
    public Text DescriptionText;
    public GameObject CameraManager;
    private ReverseCamera ReversCameraScript;
    public Transform Focus;
    public string UIText;
    private void Start()
    {
        ReversCameraScript = CameraManager.GetComponent<ReverseCamera>();
    }
    private void OnTriggerEnter(Collider other)
    {
        UIPanel.SetActive(true);
        DescriptionText.text = UIText;
        ReversCameraScript.TargetObject.localEulerAngles = Vector3.Lerp(ReversCameraScript.TargetObject.eulerAngles, Focus.eulerAngles, 1);
        //ReversCameraScript.TargetObject.LookAt(Focus);
    }
    private void OnTriggerExit(Collider other)
    {
        UIPanel.SetActive(false);
    }
}
