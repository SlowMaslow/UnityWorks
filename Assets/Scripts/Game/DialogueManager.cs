using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public GameObject PressE;
    private bool PressEtrigger;
    private bool NextStringTrigger;
    public string[] stringArr;
    private int stringCounter;
    public float letterTime;
    private float currentLetterTime;
    public Text DialogPanelText;
    public GameObject DialogPanel;
    public Collider MainCharacter;
    private bool wordstrigger;
    private int lettercounter;
    // Start is called before the first frame update
    void Start()
    {
        wordstrigger = false;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (PressEtrigger)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                MainCharacter.gameObject.GetComponent<CharacterManager>()._dialogmanager = gameObject.GetComponent<DialogueManager>();
                gameObject.GetComponent<AudioSource>().Play();
                PressE.SetActive(false);
                DialogPanelText.text = "";
                wordstrigger = true;
                stringCounter = 0;
                NextStringTrigger = true;
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {         
                if (NextStringTrigger)
                {
                    wordstrigger = false;
                    DialogPanelText.text = "";
                    lettercounter = 0;
                    stringCounter++;
                    Debug.Log(stringCounter);
                    if (stringCounter > stringArr.Length - 1)
                    {
                        DialogPanel.SetActive(false);
                        PressE.SetActive(true);
                        stringCounter = 0;
                        lettercounter = 0;
                        NextStringTrigger = false;
                        MainCharacter.gameObject.GetComponent<CharacterManager>()._dialogmanager = null;
                    }
                    else
                    {
                        wordstrigger = true;
                    }
                }
            }
        }
        if (wordstrigger)
        {
            DialogShower();
        }
    }
    private void DialogShower()
    {
        DialogPanel.SetActive(true);
        currentLetterTime += Time.deltaTime;
        if (currentLetterTime > letterTime)
        {
            DialogPanelText.text = DialogPanelText.text + stringArr[stringCounter][lettercounter];
            if (lettercounter < stringArr[stringCounter].Length - 1)
            {
                lettercounter++;
            }
            else
            {
                wordstrigger = false;
                lettercounter = 0;
            }
            currentLetterTime = 0;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other == MainCharacter)
        {
            PressE.SetActive(true);
            PressEtrigger = true;
        }       
    }
    private void OnTriggerExit(Collider other)
    {
        PressE.SetActive(false);
        PressEtrigger = false;
        DialogPanelText.text = "";
        lettercounter = 0;
        stringCounter = 0;
        DialogPanel.SetActive(false);
    }
}
