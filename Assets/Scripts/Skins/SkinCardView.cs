using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ссылки на элементы одной карточки скина в шопе. Заполняется SkinShopController'ом.
/// </summary>
public class SkinCardView : MonoBehaviour
{
    public Image      preview;        // картинка скина
    public Text       nameLabel;      // имя скина
    public Button     actionButton;   // кнопка карточки (Select / Buy)
    public Text       actionLabel;    // подпись кнопки
    public GameObject selectedFrame;  // рамка-подсветка выбранного (может быть null)
}
