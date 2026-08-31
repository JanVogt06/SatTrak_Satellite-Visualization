using UnityEngine;
using UnityEngine.UI;

public class HelpPanelController : MonoBehaviour
{
    [Header("UI-Referenzen")]
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject backButton;

    private const float fadeDuration = 0.25f;

    void Awake()
    {
        HideImmediate();
    }

    public void ShowHelp()
    {
        helpPanel.SetActive(true);
        if (backButton) backButton.SetActive(true);

        if (canvasGroup)
        {
            canvasGroup.alpha = 0;
            Fade(0, 1, fadeDuration);
        }
    }

    public void HideHelp()
    {
        if (canvasGroup)
        {
            Fade(1, 0, fadeDuration, HideImmediate);
        }
        else
        {
            HideImmediate();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && helpPanel.activeSelf)
            HideHelp();
    }

    void HideImmediate()
    {
        helpPanel.SetActive(false);
        if (backButton) backButton.SetActive(false);
    }

    void Fade(float from, float to, float time, System.Action onDone = null)
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(from, to, time, onDone));
    }

    System.Collections.IEnumerator FadeRoutine(float a, float b, float t, System.Action cb)
    {
        float e = 0f;
        while (e < t)
        {
            e += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(a, b, e / t);
            yield return null;
        }
        canvasGroup.alpha = b;
        cb?.Invoke();
    }
}
