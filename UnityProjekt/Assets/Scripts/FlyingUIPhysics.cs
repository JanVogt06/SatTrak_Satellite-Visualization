using UnityEngine;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class FlyingUIPhysics : MonoBehaviour
{
    [SerializeField]
    private Color[] palette =
        { Color.red, Color.green, Color.blue, Color.yellow };
    [SerializeField] private Vector2 speedRange = new(60f, 120f);
    [SerializeField] private float randomAngle = 10f;          // °

    private TextMeshProUGUI[] texts;
    private Image[] images;
    private Rigidbody2D rb;

    private void Awake()
    {
        texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        images = GetComponentsInChildren<Image>(true);
        rb = GetComponent<Rigidbody2D>();

        /* Anfangsimpuls */
        float v = Random.Range(speedRange.x, speedRange.y);
        float phi = Random.Range(0f, 2f * Mathf.PI);
        rb.velocity = new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * v;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        /* leichte Zufallsrotation der Geschwindigkeit */
        float theta = Random.Range(-randomAngle, randomAngle) * Mathf.Deg2Rad;
        rb.velocity = Quaternion.Euler(0, 0, theta * Mathf.Rad2Deg) * rb.velocity;

        /* Farbwechsel */
        if (palette.Length == 0) return;
        Color c = palette[Random.Range(0, palette.Length)];
        foreach (var t in texts) t.color = c;
        foreach (var im in images) im.color = c;
    }
}
