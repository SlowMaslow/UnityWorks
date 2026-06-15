using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Шоп скинов в главном меню. Строит карточки из SkinDatabase, покупка за монеты
/// (GameManager.TrySpendCoins + SaveSystem.UnlockSkin), выбор через SaveSystem.SelectedSkinId.
/// UI элементы назначаются в инспекторе; логика без прямой завязки на конкретную вёрстку.
/// </summary>
public class SkinShopController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject   panelRoot;     // корень панели шопа (вкл/выкл)
    [SerializeField] private Transform    cardContainer; // контейнер с layout под карточки
    [SerializeField] private SkinCardView cardTemplate;  // шаблон карточки (выключен, клонируется)
    [SerializeField] private Text         coinsLabel;    // баланс монет в шопе

    private readonly List<(SkinDefinition def, SkinCardView card)> _cards = new();
    private bool _built;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>Открыть шоп (кнопка Skins в меню).</summary>
    public void Open()
    {
        Build();
        Refresh();
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    /// <summary>Закрыть шоп (кнопка назад).</summary>
    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void Build()
    {
        if (_built) return;
        var db = SkinDatabase.Instance;
        if (db == null || cardTemplate == null || cardContainer == null)
        {
            Debug.LogWarning("[SkinShop] Не назначены SkinDatabase/cardTemplate/cardContainer");
            return;
        }

        cardTemplate.gameObject.SetActive(false);
        foreach (var def in db.skins)
        {
            if (def == null) continue;
            var card = Instantiate(cardTemplate, cardContainer);
            card.gameObject.SetActive(true);
            if (card.preview != null)   card.preview.sprite = def.previewSprite;
            if (card.nameLabel != null) card.nameLabel.text = def.displayName;

            var d = def; var c = card;
            if (card.actionButton != null)
                card.actionButton.onClick.AddListener(() => OnCardClicked(d, c));

            _cards.Add((def, card));
        }
        _built = true;
    }

    private void OnCardClicked(SkinDefinition def, SkinCardView card)
    {
        bool owned = IsOwned(def);

        if (!owned)
        {
            // Покупка: списываем монеты только если хватает
            if (GameManager.Instance == null || !GameManager.Instance.TrySpendCoins(def.price))
                return;
            SaveSystem.UnlockSkin(def.skinId);
        }

        // Выбор (на этот момент скин уже куплен/бесплатен)
        SaveSystem.SelectedSkinId = def.skinId;
        Refresh();
    }

    private static bool IsOwned(SkinDefinition def)
        => def.isDefault || def.price <= 0 || SaveSystem.IsSkinUnlocked(def.skinId);

    private void Refresh()
    {
        if (coinsLabel != null) coinsLabel.text = SaveSystem.Coins.ToString();

        var db = SkinDatabase.Instance;
        string selected = string.IsNullOrEmpty(SaveSystem.SelectedSkinId)
            ? (db != null ? db.GetAt(0)?.skinId : null)
            : SaveSystem.SelectedSkinId;

        foreach (var (def, card) in _cards)
        {
            bool owned = IsOwned(def);
            bool isSel = def.skinId == selected;

            if (card.selectedFrame != null) card.selectedFrame.SetActive(isSel);
            if (card.actionLabel != null)
                card.actionLabel.text = isSel ? "SELECTED"
                                      : owned ? "SELECT"
                                      : $"BUY {def.price}";
            if (card.actionButton != null)
                card.actionButton.interactable = !isSel;
        }
    }
}
