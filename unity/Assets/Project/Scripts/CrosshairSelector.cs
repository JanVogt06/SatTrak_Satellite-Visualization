using UnityEngine;
using UnityEngine.UI;

public class CrosshairSelector : MonoBehaviour
{
    [Header("Crosshair Selection")]
    public Button[] crosshairButtons;
    public Image[] crosshairImages;

    [Header("Crosshair Colors")]
    public Button[] colorButtons;
    public Color[] availableColors;

    [Header("Cursor Selection")]
    public Button[] cursorButtons;
    public Image[] cursorPreviewImages;
    public Texture2D[] cursorTextures;

    [Header("Cursor Colors")]
    public Button[] cursorColorButtons;

    public CustomCursor custCursor;

    private int currentCrosshair = 0;

    private void Start()
    {

        for (int i = 0; i < crosshairButtons.Length; i++)
        {
            int index = i;
            crosshairButtons[i].onClick.AddListener(() => SelectCrosshair(index));
        }

        for (int i = 0; i < colorButtons.Length; i++)
        {
            int index = i;
            colorButtons[i].onClick.AddListener(() => SelectColor(index));
        }

        for (int i = 0; i < cursorButtons.Length; i++)
        {
            int index = i;
            cursorButtons[i].onClick.AddListener(() => SelectCursorTexture(index));
        }

        for (int i = 0; i < cursorColorButtons.Length; i++)
        {
            int index = i;
            cursorColorButtons[i].onClick.AddListener(() => SelectCursorColor(index));
        }

        int savedCrosshair = PlayerPrefs.GetInt("CrosshairIndex", 0);
        SelectCrosshair(savedCrosshair);

        int savedCrosshairColorIndex = PlayerPrefs.GetInt("CrosshairColorIndex", 0);
        SelectColor(savedCrosshairColorIndex);

        int savedCursorIndex = PlayerPrefs.GetInt("CursorIndex", 0);
        SelectCursorTexture(savedCursorIndex);

        int savedCursorColorIndex = PlayerPrefs.GetInt("CursorColorIndex", 0);
        SelectCursorColor(savedCursorColorIndex);
    }

    void SelectCrosshair(int index)
    {
        PlayerPrefs.SetInt("CrosshairIndex", index);
        PlayerPrefs.Save();

        currentCrosshair = index;
        CrosshairSettings.selectedSprite = crosshairImages[index].sprite;

        for (int i = 0; i < crosshairButtons.Length; i++)
        {
            crosshairButtons[i].transform.GetChild(1).gameObject.SetActive(i == index);
        }
    }

    void SelectColor(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= availableColors.Length)
            return;

        PlayerPrefs.SetInt("CrosshairColorIndex", colorIndex);

        Color chosenColor = availableColors[colorIndex];
        chosenColor.a = 1f;

        CrosshairSettings.selectedColor = chosenColor;

        foreach (var img in crosshairImages)
        {
            img.color = chosenColor;
        }
    }

    public void SelectCursorTexture(int index)
    {
        PlayerPrefs.SetInt("CursorIndex", index);
        PlayerPrefs.Save();

        if (index < 0 || index >= cursorTextures.Length)
            return;

        CrosshairSettings.cursorTexture = cursorTextures[index];

        for (int i = 0; i < cursorButtons.Length; i++)
        {
            cursorButtons[i].transform.GetChild(1).gameObject.SetActive(i == index);
        }

        custCursor.ApplyCursor();
    }

    public void SelectCursorColor(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= availableColors.Length)
            return;

        PlayerPrefs.SetInt("CursorColorIndex", colorIndex);

        Color chosenColor = availableColors[colorIndex];
        chosenColor.a = 1f;

        CrosshairSettings.cursorColor = chosenColor;

        foreach (var img in cursorPreviewImages)
        {
            img.color = chosenColor;
        }

        custCursor.ApplyCursor();
    }

}
