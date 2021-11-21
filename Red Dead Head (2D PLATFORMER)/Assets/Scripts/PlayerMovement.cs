using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float jumpForce;
    [SerializeField] private float speed;
    [SerializeField] private bool isGrounded = false;
    private float verticalMovment;
    [HideInInspector] public Quaternion foward;
    [HideInInspector] public Quaternion back;

    [Header("Settings")]
    [SerializeField] private AnimationCurve curve;
    [SerializeField] private Transform groundColliderTransform;
    [SerializeField] private float jumpFactor;
    [SerializeField] private LayerMask layerMask;
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        foward = transform.rotation;
        foward.y = 0;
        back = transform.rotation;
        back.y = -180f;
        Time.timeScale = 1f;
    }
    private void FixedUpdate()
    {
        Vector3 overlapCirclePosition = groundColliderTransform.position;
        isGrounded = Physics2D.OverlapCircle(overlapCirclePosition, jumpFactor, layerMask);
        verticalMovment = rb.velocity.y;
        JumpAnimationChanger();
    }
    public void Move(float direction, bool isJumpButtonPressed)
    {
        if (isJumpButtonPressed)
        {
            Jump();
        }
        if (Mathf.Abs(direction) > 0.01f)
        {
            HorizontalMovement(direction);
            HorisontalReverse(direction);
            anim.SetBool("isRunning", true);
        }
        else
        {
            anim.SetBool("isRunning", false);
        }
    }

    private void Jump()
    {
        if (isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }
    }

    private void HorizontalMovement(float direction)
    {
        rb.velocity = new Vector2(curve.Evaluate(direction), rb.velocity.y);
    }

    private void HorisontalReverse(float direction)
    {
        if (direction >= 0)
        {
            transform.rotation = foward;
        }
        else
        {
            transform.rotation = back;
        }
    }
    private void JumpAnimationChanger()
    {
        if (!isGrounded)
        {

        }
        if (verticalMovment > 1f)
        {
            anim.SetBool("isStartJumping", true);
        }
        if (verticalMovment < -1f)
        {
            anim.SetBool("isFallJumping", true);
            anim.SetBool("isStandUp", false);
        }
        if (isGrounded)
        {
            anim.SetBool("isStartJumping", false);
            anim.SetBool("isFallJumping", false);
        }
    }

    public void FallingToIdle()
    {
        anim.SetBool("isStandUp", true);
    }

}
