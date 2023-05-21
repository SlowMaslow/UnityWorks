using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CheckButtonScript : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [HideInInspector] public bool isClicked = false;
    [HideInInspector] public bool isRealized = false;
    public void OnPointerDown(PointerEventData eventData)
    {
        isRealized = false;
        //Debug.Log(gameObject.name + " Was Clicked.");
        isClicked = true;
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        //Debug.Log(gameObject.name + " Up");
        isRealized = true;
        isClicked = false;

    }
}
