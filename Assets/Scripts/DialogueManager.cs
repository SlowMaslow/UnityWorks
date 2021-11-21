using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    [SerializeField] private string[] arrString;
    [SerializeField] private AudioClip[] audioClips;
    [SerializeField] private float[] audioClipsLength;
    private Text DialogueText;
    private float currentClipTime;
    public bool dialogueStopper;
    private bool isContacted;
    private bool isOneContact;
    private AudioSource dialoguePlayer;
    private PlayerInput playerInput;
    private Animator animChecker;
    private int i;

    private bool audioClipSet;
    private void Start()
    {
        dialoguePlayer = GetComponent<AudioSource>();
        playerInput = GameObject.Find("Main_Hero").GetComponent<PlayerInput>();
        animChecker = GameObject.Find("Main_Hero").GetComponent<Animator>();
        DialogueText = GameObject.Find("DialogueText").GetComponent<Text>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isOneContact && collision.name == "Main_Hero")
        {
            playerInput.dialogueStopper = dialogueStopper;
            isContacted = true;
            isOneContact = true;
            animChecker.SetBool("isRunning", false);
        }
        
    }
    private void Update()
    {
        if (isContacted)
        {
            DialoguePlayer();
        }
    }
    private void DialoguePlayer()
    {
        if (!audioClipSet)
        {
            dialoguePlayer.clip = audioClips[i];
            dialoguePlayer.Play();
            audioClipSet = true;
            DialogueText.text = arrString[i];
        }
        if(currentClipTime < audioClips[i].length + 1f)
        {
            currentClipTime += Time.deltaTime;
        }
        else
        {
            i++;
            if (i < audioClips.Length)
            {
                audioClipSet = false;
                currentClipTime = 0f;
            }
            else
            {
                isContacted = false;
                DialogueText.text = "";
                playerInput.dialogueStopper = false;
            }
        }  
    }
}
