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
    // Переориентирован на артефакты: HUD показывает прогресс сбора артефактов в забеге.
    // (Провизорно — финальный live-дисплей задач будет в сайдбаре, Task #5.)
    private void OnEnable()  => LevelManager.OnArtifactCollected += OnArtifact;
    private void OnDisable() => LevelManager.OnArtifactCollected -= OnArtifact;

    private void Start() => Refresh(0);

    private void OnArtifact(int collected, int total) => Refresh(collected);

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
