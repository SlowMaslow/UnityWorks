using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public void PlayingSound(string clipname)
    {
        GameObject.Find(clipname).GetComponent<AudioSource>().Play();
    }
    public void StoppingSound(string clipname)
    {
        GameObject.Find(clipname).GetComponent<AudioSource>().Stop();
    }



}
