using UnityEngine;

/// <summary>
/// Логика магазина апгрейдов в главном меню.
/// Работает только через SaveSystem и GameManager — нет прямых ссылок на UI.
/// UI обновляется через GameManager.OnCoinsChanged или явным вызовом UIController.
/// </summary>
public class UpgradeController : MonoBehaviour
{
    private UIController uiController;

    private void Start()
    {
        uiController = GetComponent<UIController>();

        if (SaveSystem.IsFirstPlay)
        {
            SaveSystem.UpgradeCost = 1;
            SaveSystem.SetFirstPlayDone();
        }

        uiController?.UpdateUpgradeCostUI();
    }

    /// <summary>Вызывается кнопкой "Upgrade" в меню.</summary>
    public void Upgrading()
    {
        if (!GameManager.Instance.TrySpendCoins(SaveSystem.UpgradeCost)) return;

        SaveSystem.UpgradeCost++;
        uiController?.UpdateUpgradeCostUI();

        // TODO: Применить эффект апгрейда (скин, параметры персонажа и т.д.)
    }
}
