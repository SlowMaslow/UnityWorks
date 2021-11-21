using UnityEngine;

public class PlayerHit : MonoBehaviour
{
    private bool hitActive;
    private bool isCombo;
    private bool nextHitAccept;
    private int currentHitNum;
    private float currentHitTime;
    private Animator anim;
    [SerializeField] private float[] HitTime;
    private int comboLength;
    // Start is called before the first frame update
    void Start()
    {
        anim = GetComponent<Animator>();
        comboLength = HitTime.Length;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (hitActive)
        {
            if (currentHitTime < HitTime[currentHitNum])
            {
                currentHitTime += Time.deltaTime;
                if (HitTime[currentHitNum] - currentHitTime < 0.4f)
                {
                    isCombo = true;
                }
            }
            else
            {
                anim.SetBool("isHit", false);
                hitActive = false;
                isCombo = false;
                currentHitTime = 0;
                currentHitNum = 0;
                anim.SetInteger("currentHit", currentHitNum);
            }
        }
    }
    public void Hit()
    {
        if (!hitActive)
        {
            anim.SetBool("isHit", true);
            hitActive = true;
        }
        if (isCombo && currentHitNum < comboLength)
        {
            nextHitAccept = true; 
        }
    }
    public void ComboHitChanger()
    {
        if (nextHitAccept)
        {
            currentHitNum++;
            anim.SetInteger("currentHit", currentHitNum);
            currentHitTime = 0;
            isCombo = false;
            nextHitAccept = false;
        }   
    }

}
