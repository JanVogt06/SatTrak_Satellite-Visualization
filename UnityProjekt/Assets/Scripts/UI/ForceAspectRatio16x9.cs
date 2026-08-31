using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class ForceAspectRatio16x9 : MonoBehaviour
{
    static readonly float targetRatio = 16f / 9f;
    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        ApplyAspect();
    }

    void OnPreCull() => ApplyAspect();
    void OnRectTransformDimensionsChange() => ApplyAspect();

    void ApplyAspect()
    {
        float currentRatio = (float)Screen.width / Screen.height;
        if (Mathf.Approximately(currentRatio, targetRatio))
        {
            cam.rect = new Rect(0, 0, 1, 1);
            return;
        }

        if (currentRatio > targetRatio)
        {
            float inset = 1f - targetRatio / currentRatio;
            cam.rect = new Rect(inset * 0.5f, 0, 1f - inset, 1);
        }
        else
        {
            float inset = 1f - currentRatio / targetRatio;
            cam.rect = new Rect(0, inset * 0.5f, 1, 1f - inset);
        }
    }
}
