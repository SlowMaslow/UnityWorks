using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class BossTrigger : MonoBehaviour
{
    [SerializeField] GameObject Boss;
    [SerializeField] CinemachineVirtualCamera virtualCamera;
    private PlayerInput playerInput;
    private bool CheckOnce;
    void Start()
    {
        playerInput = GameObject.Find("Main_Hero").GetComponent<PlayerInput>();
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.name == "Main_Hero")
        {
            if (playerInput.dialogueStopper != true)
            {
                virtualCamera.Follow = null;
                Boss.GetComponent<Animator>().SetBool("Fight", true);
                gameObject.SetActive(false);
            }
        }      
    }
}
