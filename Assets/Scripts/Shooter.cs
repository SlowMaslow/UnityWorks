using UnityEngine;

public class Shooter : MonoBehaviour
{
    [SerializeField] private GameObject bullet;
    [SerializeField] private float fireSpeed;
    [SerializeField] private Transform firePoint;
    private PlayerAttack playerAttack;
    private PlayerMovement playerMovement;
    private void Awake()
    {
        playerAttack = GetComponent<PlayerAttack>();
        playerMovement = GetComponent<PlayerMovement>();
    }
    public void Shoot(float direction)
    {
        GameObject currentBullet = Instantiate(bullet, firePoint.position, Quaternion.identity);
        Rigidbody2D currentBulletVelocity = currentBullet.GetComponent<Rigidbody2D>();
        if (transform.rotation == playerMovement.foward)
        {
            currentBullet.transform.rotation = playerMovement.foward;
            currentBulletVelocity.velocity = new Vector2((fireSpeed * playerAttack.currentStretchingTime) * 1, currentBulletVelocity.velocity.y);
        }
        else
        {
            currentBullet.transform.rotation = playerMovement.back;
            currentBulletVelocity.velocity = new Vector2((fireSpeed * playerAttack.currentStretchingTime) * -1, currentBulletVelocity.velocity.y);
        }          
    }
}
