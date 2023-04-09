using UnityEngine;

public class FailCollider : MonoBehaviour
{
    [SerializeField] private GameObject FallWindow;
    [SerializeField] private GameObject PauseButton;
    [SerializeField] private Rigidbody[] dolling;
    private void Start()
    {
        PauseButton = GameObject.Find("Pause");
    }
    private void OnCollisionEnter(Collision collision)
    {
        FallWindow.SetActive(true);
        PauseButton.SetActive(false);
        dolling[0].constraints = RigidbodyConstraints.None;
        dolling[1].constraints = RigidbodyConstraints.None;
    }
}
