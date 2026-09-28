using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Independent module selection in six wardrobe slots.</summary>
public class SkinShopController : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private SkinCardView cardTemplate;
    [SerializeField] private Text coinsLabel;
    private readonly List<(SkinAccessory item, SkinCardView card)> cards = new();
    private readonly Button[] tabs = new Button[SkinWardrobe.SlotCount];
    private readonly string[] labels = { "HAIR / HATS", "HEAD", "GLASSES", "TORSO", "LEGS", "FEET" };
    private readonly SkinAccessorySlot[] slotOrder = { SkinAccessorySlot.Headwear, SkinAccessorySlot.Head, SkinAccessorySlot.Glasses, SkinAccessorySlot.Outerwear, SkinAccessorySlot.Pants, SkinAccessorySlot.Footwear };
    private SkinAccessorySlot activeSlot;
    private Text statusLabel;
    private ScrollRect scroll;
    private bool built;

    private void Awake() { if (panelRoot != null) panelRoot.SetActive(false); }
    private void OnEnable()
    {
        SaveSystem.AccessoriesChanged += Refresh;
        GameManager.OnCoinsChanged += OnCoinsChanged;
    }
    private void OnDisable()
    {
        SaveSystem.AccessoriesChanged -= Refresh;
        GameManager.OnCoinsChanged -= OnCoinsChanged;
    }
    private void OnCoinsChanged(int _) => Refresh();
    public void Open()
    {
        Build();
        Refresh();
        if (panelRoot != null) panelRoot.SetActive(true);
    }
    public void Close() { if (panelRoot != null) panelRoot.SetActive(false); }

    private void Build()
    {
        if (built) return;
        if (SkinAccessoryDatabase.Instance == null || panelRoot == null || cardTemplate == null || cardContainer == null)
        {
            Debug.LogWarning("[SkinShop] Missing accessory catalog or UI references.", this);
            return;
        }
        cardTemplate.gameObject.SetActive(false);
        var title = panelRoot.transform.Find("Title")?.GetComponent<Text>();
        if (title != null) title.text = "WARDROBE";
        var bar = new GameObject("AccessoryCategories", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(panelRoot.transform, false);
        bar.anchorMin = bar.anchorMax = new Vector2(.5f, 1);
        bar.pivot = new Vector2(.5f, 1); bar.sizeDelta = new Vector2(580, 44);
        bar.anchoredPosition = new Vector2(0, -94);
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var button = Instantiate(cardTemplate.actionButton, bar);
            button.name = labels[i]; button.gameObject.SetActive(true);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => SelectSlot(slotOrder[index]));
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(i / 6f, 0); rect.anchorMax = new Vector2((i + 1) / 6f, 1);
            rect.offsetMin = new Vector2(4, 0); rect.offsetMax = new Vector2(-4, 0);
            var text = button.GetComponentInChildren<Text>(true);
            text.text = labels[i]; text.fontSize = 17; text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12; text.resizeTextMaxSize = 17;
            tabs[i] = button;
        }
        var content = (RectTransform)cardContainer;
        var viewport = new GameObject("AccessoryViewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
        viewport.SetParent(panelRoot.transform, false);
        viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
        viewport.sizeDelta = new Vector2(580, 270); viewport.anchoredPosition = new Vector2(0, -46);
        content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var grid = content.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180, 250); grid.spacing = new Vector2(10, 10);
        grid.padding = new RectOffset(10, 10, 10, 10); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;
        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = viewport.GetComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25;
        var status = new GameObject("WardrobeStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        status.transform.SetParent(panelRoot.transform, false); statusLabel = status.GetComponent<Text>();
        statusLabel.font = cardTemplate.actionLabel.font; statusLabel.fontSize = 17;
        statusLabel.alignment = TextAnchor.MiddleCenter; statusLabel.color = Color.white; statusLabel.raycastTarget = false;
        var statusRect = (RectTransform)status.transform;
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(.5f, 0); statusRect.pivot = new Vector2(.5f, 0);
        statusRect.sizeDelta = new Vector2(580, 30); statusRect.anchoredPosition = new Vector2(0, 18);
        built = true;
        SelectSlot(SkinAccessorySlot.Headwear);
    }

    public void SelectSlot(SkinAccessorySlot slot)
    {
        if (!built || (int)slot < 0 || (int)slot >= SkinWardrobe.SlotCount) return;
        activeSlot = slot;
        foreach (var entry in cards)
        {
            entry.card.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(entry.card.gameObject); else DestroyImmediate(entry.card.gameObject);
        }
        cards.Clear();
        AddCard(null);
        foreach (var item in SkinAccessoryDatabase.Instance.GetSlot(slot)) AddCard(item);
        if (statusLabel != null) statusLabel.text = "Choose an item for this slot. Other slots stay equipped.";
        if (scroll != null) scroll.verticalNormalizedPosition = 1;
        Refresh();
    }

    private void AddCard(SkinAccessory item)
    {
        var card = Instantiate(cardTemplate, cardContainer); card.gameObject.SetActive(true);
        card.name = item == null ? "EmptySlot" : item.accessoryId;
        if (card.preview != null)
        {
            card.preview.sprite = item != null ? item.previewSprite : SkinAccessoryDatabase.Instance.GetBasePreview(activeSlot);
            card.preview.preserveAspect = true; card.preview.raycastTarget = false;
        }
        if (card.nameLabel != null) card.nameLabel.text = item != null ? item.displayName : SkinWardrobe.RegionForSlot(activeSlot)>=0 ? "Base" : "None";
        if (card.selectedFrame != null)
            foreach (var graphic in card.selectedFrame.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        if (card.actionButton != null)
        {
            card.actionButton.onClick = new Button.ButtonClickedEvent();
            card.actionButton.onClick.AddListener(() => OnCardClicked(item));
        }
        cards.Add((item, card));
    }

    private void OnCardClicked(SkinAccessory item)
    {
        if (item == null)
        {
            AccessoryShop.Clear(activeSlot);
            statusLabel.text = SkinWardrobe.RegionForSlot(activeSlot)>=0 ? "Base module restored." : "Slot cleared.";
        }
        else if (AccessoryShop.TrySelect(item, out var error)) statusLabel.text = item.displayName + " equipped.";
        else statusLabel.text = error;
        Refresh();
    }

    private void Refresh()
    {
        if (coinsLabel != null) coinsLabel.text = SaveSystem.Coins.ToString();
        if (!built) return;
        var selected = SkinAccessoryDatabase.Instance.Get(SaveSystem.GetSelectedAccessory(activeSlot));
        if (selected != null && (selected.slot != activeSlot || !AccessoryShop.IsOwned(selected))) selected = null;
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].interactable = slotOrder[i] != activeSlot;
            var text = tabs[i].GetComponentInChildren<Text>();
            bool hasItem = SkinAccessoryDatabase.Instance.Get(SaveSystem.GetSelectedAccessory(slotOrder[i])) != null;
            text.text = labels[i] + (hasItem ? " *" : "");
        }
        foreach (var (item, card) in cards)
        {
            bool isSelected = selected == item;
            bool owned = item == null || AccessoryShop.IsOwned(item);
            if (card.selectedFrame != null) card.selectedFrame.SetActive(isSelected);
            if (card.actionLabel != null) card.actionLabel.text = isSelected ? "SELECTED" : item == null ? (SkinWardrobe.RegionForSlot(activeSlot)>=0 ? "RESTORE" : "REMOVE") : owned ? "EQUIP" : $"BUY {item.price}";
            if (card.actionButton != null) card.actionButton.interactable = !isSelected;
        }
    }
}
