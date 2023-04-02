using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndLevelScript : MonoBehaviour
{
    [SerializeField] private GameObject FailWindow;
    [SerializeField] private Transform ParticlePoint;
    [SerializeField] private GameObject Particle;
    private TimeManager timeManager;
    private Rigidbody sphere;
    [HideInInspector] public bool Fail;
    private void Start()
    {
        sphere = FindObjectOfType<InputListener>().GetComponent<Rigidbody>();
        timeManager = FindObjectOfType<TimeManager>();
    }
    void Update()
    {
        
    }
    public void EndLevel()
    {
        FailWindow.SetActive(true);
        timeManager.WinTrigger = true;
        CreateParticles();
        sphere.isKinematic = true;
    }
    private void CreateParticles()
    {
        Instantiate(Particle, ParticlePoint.position, Quaternion.identity, FailWindow.transform);
    }
}
