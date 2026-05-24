using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Добавляется на любую кнопку. При нажатии воспроизводит звук через SoundManager.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(() => SoundManager.Instance?.PlayButtonSound());
    }
}
