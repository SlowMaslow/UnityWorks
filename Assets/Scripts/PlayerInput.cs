using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Shooter))]
[RequireComponent(typeof(PlayerAttack))]
public class PlayerInput : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private Shooter shooter;
    private PlayerAttack playerAttack;
    private PlayerHit playerHit;
    private SoundManager soundManager;
    private Health health;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private FixedJoystick fixedJoystick;
    [SerializeField] private Button bowButton;
    [SerializeField] private Button hitButton;
    [SerializeField] private Button jumpButton;
    [SerializeField] private Button pauseButton;

    [HideInInspector] public bool dialogueStopper;
    private bool PauseSwitcher;
    private MobileCheck mobileCheck;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        shooter = GetComponent<Shooter>();
        playerAttack = GetComponent<PlayerAttack>();
        playerHit = GetComponent<PlayerHit>();
        health = GetComponent<Health>();
        soundManager = GetComponent<SoundManager>();
        mobileCheck = FindObjectOfType<MobileCheck>();
    }
    private void Start()
    {
        if (mobileCheck.isMobile == 1)
        {
            fixedJoystick.gameObject.SetActive(true);
            bowButton.gameObject.SetActive(true);
            hitButton.gameObject.SetActive(true);
            jumpButton.gameObject.SetActive(true);
            pauseButton.gameObject.SetActive(true);
        }
        else
        {
            fixedJoystick.gameObject.SetActive(false);
            bowButton.gameObject.SetActive(false);
            hitButton.gameObject.SetActive(false);
            jumpButton.gameObject.SetActive(false);
            pauseButton.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        float horizontalDirection;
        bool isJumpButtonPressed;
        bool isFireButtonPressed;
        bool isHitButtonPressed;
        bool isPauseButtonPressed;
        if (mobileCheck.isMobile == 1)
        {
            horizontalDirection = fixedJoystick.Horizontal;
            isJumpButtonPressed = jumpButton.GetComponent<CheckButtonScript>().isClicked;
            isFireButtonPressed = bowButton.GetComponent<CheckButtonScript>().isClicked;
            isHitButtonPressed = false;
            isPauseButtonPressed = pauseButton.GetComponent<CheckButtonScript>().isRealized;
        }
        else
        {
            horizontalDirection = Input.GetAxis(GlobalStringVars.HORIZONTAL_AXIS);
            isJumpButtonPressed = Input.GetButtonDown(GlobalStringVars.JUMP);
            isFireButtonPressed = Input.GetButton(GlobalStringVars.FIRE_1);
            isHitButtonPressed = Input.GetButtonDown(GlobalStringVars.FIRE_2);
            isPauseButtonPressed = Input.GetButtonDown(GlobalStringVars.PAUSE);
        }


        if (!pausePanel.activeSelf && health.isAlive && !dialogueStopper)
        {
            if (bowButton.GetComponent<CheckButtonScript>().isRealized || (mobileCheck.isMobile != 1 && Input.GetButtonUp(GlobalStringVars.FIRE_1)))
            {
                soundManager.StoppingSound("bow_stretch");
                soundManager.PlayingSound("bow_fly");
                shooter.Shoot(horizontalDirection);
                bowButton.GetComponent<CheckButtonScript>().isRealized = false;
            }
            if (isHitButtonPressed)
            {
                playerHit.Hit();
            }
            playerMovement.Move(horizontalDirection, isJumpButtonPressed);
            playerAttack.BowStretch(isFireButtonPressed);
        }
        PauseButtonSwitcher(isPauseButtonPressed);
    }

    private void PauseButtonSwitcher(bool isPauseButtonPressed)
    {
        if (isPauseButtonPressed)
        {
            pauseButton.GetComponent<CheckButtonScript>().isRealized = false;
            if (!PauseSwitcher)
            {
                PauseSwitcher = true;
                pausePanel.SetActive(true);
                Time.timeScale = 0;
            }
            else
            {
                PauseSwitcher = false;
                pausePanel.SetActive(false);
                Time.timeScale = 1;
            }
        }       
    }
}
