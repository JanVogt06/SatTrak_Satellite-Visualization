using UnityEngine;
using UnityEngine.UI;

public class TabBarController : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        [Tooltip("Toggle-Reiter dieses Tabs")]
        public Toggle toggle;

        [Tooltip("Content-Panel, das zu diesem Tab gehört")]
        public GameObject panel;

        [Tooltip("(Optional) Graphic, das farblich hervorgehoben wird – z. B. Background-Image oder Underline-Image")]
        public Graphic highlight;
    }

    [Header("Tabs in derselben Reihenfolge wie in der TabBar")]
    [SerializeField] private Tab[] tabs;

    [Header("Highlight-Farben")]
    [SerializeField] private Color activeColor   = new Color(0f, 0.88f, 1f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.16f, 0.16f, 0.16f);

    private void Awake()
    {

        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) ActivateTab(index);
            });
        }

        tabs[0].toggle.isOn = true;
        ActivateTab(0);
    }

    private void ActivateTab(int activeIndex)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            bool isActive = (i == activeIndex);

            if (tabs[i].panel != null)
                tabs[i].panel.SetActive(isActive);

            if (tabs[i].highlight != null)
                tabs[i].highlight.color = isActive ? activeColor : inactiveColor;
        }
    }
}
