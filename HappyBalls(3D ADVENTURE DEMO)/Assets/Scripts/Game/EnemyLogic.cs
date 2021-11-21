using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyLogic : MonoBehaviour
{
    public Transform MainCharacter;
    private Transform selfTransform;
    public ParticleSystem deathparticle;
    public Animator anim;
    private bool deathBool;
    [SerializeField] private float power;
    [SerializeField] private AudioSource death;
    [SerializeField] private AudioSource ouch;
    void Start()
    {
        selfTransform = GetComponent<Transform>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        selfTransform.position = Vector3.Lerp(selfTransform.position, new Vector3(MainCharacter.position.x, selfTransform.position.y, MainCharacter.position.z), 0.005f);
        selfTransform.LookAt(new Vector3(MainCharacter.position.x, selfTransform.position.y, MainCharacter.position.z));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<HealthManager>())
        {
            if (collision.gameObject.GetComponent<HealthManager>() && !collision.gameObject.GetComponent<HealthManager>().immortal && collision.contacts[0].thisCollider.name != "KillCollider")
            {
                Vector3 direction = collision.transform.position - transform.position;
                collision.rigidbody.AddForce(direction.normalized * power, ForceMode.Impulse);
                collision.gameObject.GetComponent<HealthManager>().health = collision.gameObject.GetComponent<HealthManager>().health - 1;
                collision.gameObject.GetComponent<HealthManager>().immortal = true;
                ouch.Play();
            }
            if (collision.contacts[0].thisCollider.name == "KillCollider" && !deathBool)
            {
                Vector3 direction = collision.transform.position - transform.position;
                collision.rigidbody.AddForce(direction.normalized * (power), ForceMode.Impulse);
                deathBool = true;
                deathparticle.Play();
                death.Play();
                anim.SetBool("death", true);
            }
        }   
    }
}
