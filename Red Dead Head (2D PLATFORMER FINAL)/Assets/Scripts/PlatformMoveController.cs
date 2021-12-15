using UnityEngine;

public class PlatformMoveController : MonoBehaviour
{
    private Animator anim;
    [SerializeField] private int PlatformState;
    void Start()
    {
        anim = GetComponent<Animator>();
        anim.SetInteger("PlatformState", PlatformState);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
