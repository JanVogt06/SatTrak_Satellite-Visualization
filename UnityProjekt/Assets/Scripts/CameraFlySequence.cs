using System.Collections;
using UnityEngine;

/// <summary>
/// Fährt die Kamera vom rotierenden Start-Orbit zu einer festen UI-Nah­ansicht
/// (Z-Koordinate = fixedTargetZ) und wieder zurück. Während des UI-Aufenthalts
/// ist die Drehung deaktiviert.
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class CameraFlySequence : MonoBehaviour
{
    [Header("Ziel-Transform (X/Y-Position + Rotation)")]
    public Transform uiAnchor;

    [Header("Fester Z-Wert für Nahansicht")]
    public float fixedTargetZ = -987f;

    [Header("Fahrtdauer hin- und zurück (s)")]
    public float travelTime = 2.5f;

    [Header("Glättung (0 = linear, 2–5 = weich)")]
    [Range(0f, 5f)]
    public float smoothness = 2f;

    /* ---------- interne Felder ---------- */
    private MainMenuCameraMovement orbitScript;
    private Vector3 startPos;
    private Quaternion startRot;

    /* ---------- Initialisierung ---------- */
    private void Awake()
    {
        orbitScript = GetComponent<MainMenuCameraMovement>();
        startPos = transform.position;
        startRot = transform.rotation;
    }

    /* ---------- öffentliche Trigger ---------- */

    /// <summary>Startet die Fahrt zur UI-Ansicht.</summary>
    /* ---------- Pufferspeicher für Rückfahrt ---------- */
    private Vector3 orbitResumePos;
    private Quaternion orbitResumeRot;

    /* ---------- Startet die Fahrt zur UI-Ansicht ---------- */
    public void FlyToUI()
    {
        /* 1) aktuelle Orbit-Pose sichern  */
        orbitResumePos = transform.position;
        orbitResumeRot = transform.rotation;

        /* 2) Zielkoordinate bilden (X/Y vom Anchor, fester Z) */
        Vector3 uiPos = uiAnchor.position;
        uiPos.z = fixedTargetZ;
        uiPos.y = 1023f;                             // (falls gewünscht)

        /* 3) Coroutine mit **aktueller** Pose starten        */
        StartCoroutine(Fly(transform.position, transform.rotation,
                           uiPos, uiAnchor.rotation,
                           disableOrbit: true));
    }

    /* ---------- Rückflug zur gespeicherten Orbit-Pose ---- */
    public void FlyBack()
    {
        StartCoroutine(Fly(transform.position, transform.rotation,
                           orbitResumePos, orbitResumeRot,
                           disableOrbit: false));
    }

    /* ---------- Kern-Coroutine ---------- */
    private IEnumerator Fly(Vector3 fromPos, Quaternion fromRot,
                            Vector3 toPos, Quaternion toRot,
                            bool disableOrbit)
    {
        orbitScript.enabled = !disableOrbit;               // Orbit ggf. deaktivieren
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / travelTime;

            /* NEU: kubische Ease-In-Out-Funktion                     */
            /* 0 – 1-Bereich beibehalten, aber sanfter Start/Ende    */
            float s = EaseInOutCubic(Mathf.Clamp01(t));

            transform.position = Vector3.Lerp(fromPos, toPos, s);
            transform.rotation = Quaternion.Slerp(fromRot, toRot, s);
            yield return null;
        }
        orbitScript.enabled = disableOrbit ? false : true;
    }

    /* ---------- kubische Ease-In-Out-Kurve ---------- */
    private static float EaseInOutCubic(float x)
    {
        return (x < 0.5f)
            ? 4f * x * x * x                       // Beschleunigungs­phase
            : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f; // Abbremsphase
    }
}
