using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipController : MonoBehaviour,
                                  IPointerEnterHandler,
                                  IPointerExitHandler
{
    [SerializeField]
    private GameObject infoPanel;

    private void Awake()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (infoPanel != null)
            infoPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    public void HasClicked()
    {
        EventSystem.current.SetSelectedGameObject(null);
    }
}
