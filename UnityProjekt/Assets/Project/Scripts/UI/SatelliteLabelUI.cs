using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SatelliteLabelUI : MonoBehaviour
{
    public TextMeshProUGUI labelText;
    public Transform target;
    public Camera mainCamera;

    public SearchPanelController controller;

    private void Awake()
    {
        if (TryGetComponent(out Button btn))
            btn.onClick.AddListener(OnLabelClicked);
    }

    public void SetTarget(Transform satellite, string name)
    {
        target = satellite;
        labelText.text = name;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        target = null;
    }

    private void Update()
    {
        if (target == null || mainCamera == null)
        {
            Hide();
            return;
        }
    }

    private void OnLabelClicked()
    {
        if (controller != null && labelText != null)
        {
            controller.OnItemSelected(labelText.text);
            this.gameObject.SetActive(false);
        }
    }
}
