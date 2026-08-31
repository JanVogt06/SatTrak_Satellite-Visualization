using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    public Vector2 hotspot = Vector2.zero;
    public CursorMode cursorMode = CursorMode.Auto;

    private Texture2D lastCursorTexture;

    void Start()
    {
        ApplyCursor();
    }

    public void ApplyCursor()
    {
        if (CrosshairSettings.cursorTexture == null)
        {
            Debug.LogWarning("No cursor texture assigned!");
            return;
        }

        Texture2D coloredTexture = TintCursorTexture(CrosshairSettings.cursorTexture, CrosshairSettings.cursorColor);

        Cursor.SetCursor(coloredTexture, hotspot, cursorMode);

        if (lastCursorTexture != null)
            Destroy(lastCursorTexture);
        lastCursorTexture = coloredTexture;
    }

    Texture2D TintCursorTexture(Texture2D original, Color color)
    {
        Texture2D newTex = new Texture2D(original.width, original.height, TextureFormat.RGBA32, false);
        newTex.filterMode = original.filterMode;
        newTex.wrapMode = original.wrapMode;

        Color[] pixels = original.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color * pixels[i];
        }

        newTex.SetPixels(pixels);
        newTex.Apply();
        return newTex;
    }

    private void OnDestroy()
    {
        if (lastCursorTexture != null)
            Destroy(lastCursorTexture);
    }
}
