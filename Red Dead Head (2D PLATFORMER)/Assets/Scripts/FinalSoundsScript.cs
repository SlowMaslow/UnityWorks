using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FinalSoundsScript : MonoBehaviour
{
    [SerializeField] private AudioSource MainMusic;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        GetComponent<AudioSource>().Play();
        MainMusic.Stop();
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        gameObject.SetActive(false);
    }
}
