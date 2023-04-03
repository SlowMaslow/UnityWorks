
using UnityEngine;

public class InputListener : MonoBehaviour
{
    Vector2 startPos;
    Vector2 endPos;
    private Rigidbody rb;
    private TimeManager timeManager;
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        Physics.gravity = new Vector3(0, -10, 0);
        timeManager = FindObjectOfType<TimeManager>();
    }

    private void Update()
    {
        if (!timeManager.Paused)
        {
            if (Input.touchCount == 1)
            {
                swipeListener();
            }
            buttonsListener();
        }
    }

    private void swipeListener()
    {
        Touch touch = Input.GetTouch(0);
        if (touch.phase == TouchPhase.Began)
        {
            startPos = touch.position;
        }
        if (touch.phase == TouchPhase.Ended)
        {
            endPos = touch.position;
            Vector2 differentPos = startPos - endPos;
            if (Mathf.Abs(differentPos.y) < 70)
            {
                if (differentPos.x > 100)
                {
                    //Debug.Log("Свайп влево");
                    Physics.gravity = new Vector3(-10, 0, 0);
                    rb.velocity = Vector3.zero;
                }
                if (differentPos.x < -100)
                {
                    //Debug.Log("Свайп вправо");
                    Physics.gravity = new Vector3(10, 0, 0);
                    rb.velocity = Vector3.zero;
                }
            }
            if (Mathf.Abs(differentPos.x) < 700)
            {
                if (differentPos.y > 100)
                {
                    //Debug.Log("Свайп вниз");
                    Physics.gravity = new Vector3(0, 0, -10);
                    rb.velocity = Vector3.zero;
                }
                if (differentPos.y < -100)
                {
                    //Debug.Log("Свайп вверх");
                    Physics.gravity = new Vector3(0, 0, 10);
                    rb.velocity = Vector3.zero;
                }
            }
        }
    }

    private void buttonsListener()
    {

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Debug.Log("Свайп влево");
            Physics.gravity = new Vector3(-10, 0, 0);
            rb.velocity = Vector3.zero;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            //Debug.Log("Свайп вправо");
            Physics.gravity = new Vector3(10, 0, 0);
            rb.velocity = Vector3.zero;
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            //Debug.Log("Свайп вниз");
            Physics.gravity = new Vector3(0, 0, -10);
            rb.velocity = Vector3.zero;
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            //Debug.Log("Свайп вверх");
            Physics.gravity = new Vector3(0, 0, 10);
            rb.velocity = Vector3.zero;
        }
    }
}
