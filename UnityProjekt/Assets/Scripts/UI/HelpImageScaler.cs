using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(LayoutElement))]
public class HelpImageScaler : MonoBehaviour, ILayoutSelfController
{
    [Range(0.1f, 1f)]
    [SerializeField] float maxRelativeWidth = 0.8f;

    Image         img;
    LayoutElement le;
    RectTransform rt;

    void Awake()
    {
        img = GetComponent<Image>();
        le  = GetComponent<LayoutElement>();
        rt  = GetComponent<RectTransform>();

        if (img) img.preserveAspect = true;
    }

    public void SetLayoutHorizontal() => Adjust();
    public void SetLayoutVertical()   => Adjust();

    void Adjust()
    {

        if (img == null || le == null || rt == null || img.sprite == null)
            return;

        RectTransform parentRT = rt.parent as RectTransform;
        if (parentRT == null) return;

        float maxW = parentRT.rect.width * maxRelativeWidth;

        float spriteW = img.sprite.rect.width;
        float spriteH = img.sprite.rect.height;
        float ratio   = spriteH / spriteW;

        float targetW = Mathf.Min(maxW, spriteW);
        float targetH = targetW * ratio;

        le.preferredWidth  = targetW;
        le.preferredHeight = targetH;
    }
}
