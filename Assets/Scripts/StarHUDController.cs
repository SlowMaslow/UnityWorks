using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Управляет отображением 3 звёзд в HUD уровня.
/// Звёзды серые по умолчанию, заполняются золотом при сборе.
/// Привязывается к GameObject с 3 дочерними Text-элементами.
/// </summary>
public class StarHUDController : MonoBehaviour
{
    [SerializeField] private Text[] starTexts; // 3 элемента, заполняются в Inspector

    private static readonly Color ColorEmpty  = new Color(1f, 1f, 1f, 0.30f);
    private static readonly Color ColorFilled = new Color(1f, 0.82f, 0.05f, 1f);
    private const string STAR_CHAR = "★";

    // ─── Unity ───────────────────────────────────────────────────────────────
    private void OnEnable()  => LevelManager.OnStarCollected += Refresh;
    private void OnDisable() => LevelManager.OnStarCollected -= Refresh;

    private void Start() => Refresh(0);

    // ─── Private ─────────────────────────────────────────────────────────────
    private void Refresh(int filledCount)
    {
        for (int i = 0; i < starTexts.Length; i++)
        {
            if (starTexts[i] == null) continue;
            starTexts[i].text  = STAR_CHAR;
            starTexts[i].color = i < filledCount ? ColorFilled : ColorEmpty;
        }
    }
}
