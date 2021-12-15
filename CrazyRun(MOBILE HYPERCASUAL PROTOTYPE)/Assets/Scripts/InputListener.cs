
using UnityEngine;

public class InputListener : MonoBehaviour
{
    Vector2 startPos;
    Vector2 endPos;
    private Rigidbody rb;
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        Physics.gravity = new Vector3(0, -10, 0);
    }

    private void Update()
    {
        if (Input.touchCount == 1)
        {
            swipeListener();
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
                    Physics.gravity = new Vector3(0, 0, 10);
                    rb.velocity = Vector3.zero;
                }
            }
        }
    }
}
