using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class enemyHealthBarBehavior : MonoBehaviour
{
    [SerializeField] private GameObject enemy_UI;
    [SerializeField] private Vector3 offset;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        enemy_UI.transform.position = Camera.main.WorldToScreenPoint(transform.parent.position + offset);
    }
}
