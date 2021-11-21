using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterManager : MonoBehaviour
{
    public GameObject Ball;
    public Rigidbody selfRigid;
    public float speed = 20f;
    public float jumpStrenght = 5f; 
    public Transform direction;
    public Camera MainCamera;
    public ParticleSystem jumpparticle;
    public AudioSource jumpSound;
    private bool isGrounded;
    public Animator anim;
    private float lerpTime;
    private Quaternion startRotate;
    public GameObject MaterialChanger;
    private Renderer rend;
    public DialogueManager _dialogmanager;

    // Start is called before the first frame update
    void Start()
    {
        startRotate = selfRigid.rotation;
        lerpTime = 0.01f;
        Ball.GetComponent<Renderer>().sharedMaterial = MaterialChanger.GetComponent<MaterialScript>().MaterialArr[PlayerPrefs.GetInt("Element")];
    }

    // Update is called once per frame
    void Update()
    {
        if(_dialogmanager == null)
        {
            if (Input.GetKey(KeyCode.W))
            {
                selfRigid.AddForce(MainCamera.transform.forward * speed * Time.deltaTime);
            }
            if (Input.GetKey(KeyCode.S))
            {
                selfRigid.AddForce(MainCamera.transform.forward * -speed * Time.deltaTime);
            }
            if (Input.GetKey(KeyCode.D))
            {
                selfRigid.AddForce(MainCamera.transform.right * speed * Time.deltaTime);
            }
            if (Input.GetKey(KeyCode.A))
            {
                selfRigid.AddForce(MainCamera.transform.right * -speed * Time.deltaTime);
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (isGrounded)
                {
                    jumpparticle.Play();
                    jumpSound.Play();
                    selfRigid.AddForce(direction.transform.up * jumpStrenght, ForceMode.Impulse);
                }
            }
        }       
        if ((selfRigid.angularVelocity.x < -0.5 || selfRigid.angularVelocity.x > 0.5) || (selfRigid.angularVelocity.z < -0.5 || selfRigid.angularVelocity.z > 0.5))
        {
            //anim.SetBool("moving", true);
           // anim.enabled = false;
        }
        else
        {
            if (lerpTime < 1.5f)
            {
                selfRigid.rotation = Quaternion.Lerp(selfRigid.rotation, startRotate, 0.2f);
                lerpTime += Time.deltaTime;
            }
            else
            {
                lerpTime = 0.01f;
                //anim.enabled = true;
                //anim.SetBool("moving", false);
            }
        }
        //Debug.Log(isGrounded);
        startRotate.eulerAngles = direction.rotation.eulerAngles;
        direction.position = selfRigid.gameObject.transform.position;
    }
    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.tag == "ground")
        {
            isGrounded = true;
        }
        //if (collision.gameObject == GameObject.Find("Platform"))
    }
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.tag == "ground")
        {
            isGrounded = false;
        }
    }

}
