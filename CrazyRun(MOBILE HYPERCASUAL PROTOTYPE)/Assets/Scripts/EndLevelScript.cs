using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndLevelScript : MonoBehaviour
{
    [SerializeField] private GameObject FailWindow;
    [SerializeField] private Transform ParticlePoint;
    [SerializeField] private GameObject Particle;
    [HideInInspector] public bool Fail;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void EndLevel()
    {
        FailWindow.SetActive(true);
        CreateParticles();
    }
    private void CreateParticles()
    {
        Instantiate(Particle, ParticlePoint.position, Quaternion.identity, FailWindow.transform);
    }
}
