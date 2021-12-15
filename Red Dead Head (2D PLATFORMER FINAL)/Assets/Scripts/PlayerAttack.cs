using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private float MaxStretchingTime;
    public float currentStretchingTime;
    [HideInInspector] public bool MaxBowStretchAchived;
    //public ParticleSystem particleSystem;
    private Animator anim;
    private void Awake()
    {
        anim = GetComponent<Animator>();
    }
    public void BowStretch(bool isFireButtonPressed)
    {
        if (isFireButtonPressed)
        {
            anim.SetBool("isStretching", true);
            currentStretchingTime += Time.deltaTime;
            if (currentStretchingTime > MaxStretchingTime)
            {
                currentStretchingTime = MaxStretchingTime;
                MaxBowStretchAchived = true;
                //particleSystem.Play();
            }
        }
        else
        {
            currentStretchingTime = 0;
            MaxBowStretchAchived = false;
            anim.SetBool("isStretching", false);
        }

    }
    public void isShootingStart()
    {
        anim.SetBool("isShootingEnd", false);
    }
    public void isShootingEnd()
    {
        anim.SetBool("isShootingEnd", true);
    }
    public void isDamage()
    {
        anim.SetBool("isDamage", false);
    }
}
