using UnityEngine;

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
    [HideInInspector] public bool dialogueStopper;
    private bool PauseSwitcher;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        shooter = GetComponent<Shooter>();
        playerAttack = GetComponent<PlayerAttack>();
        playerHit = GetComponent<PlayerHit>();
        health = GetComponent<Health>();
        soundManager = GetComponent<SoundManager>();
    }
    private void Update()
    {
        float horizontalDirection = Input.GetAxis(GlobalStringVars.HORIZONTAL_AXIS);
        bool isJumpButtonPressed = Input.GetButtonDown(GlobalStringVars.JUMP);
        bool isFireButtonPressed = Input.GetButton(GlobalStringVars.FIRE_1);
        bool isHitButtonPressed = Input.GetButtonDown(GlobalStringVars.FIRE_2);
        bool isPauseButtonPressed = Input.GetButtonDown(GlobalStringVars.PAUSE);

        if (!pausePanel.activeSelf && health.isAlive && !dialogueStopper)
        {
            if (Input.GetButtonUp(GlobalStringVars.FIRE_1))
            {
                soundManager.StoppingSound("bow_stretch");
                soundManager.PlayingSound("bow_fly");
                shooter.Shoot(horizontalDirection);
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
