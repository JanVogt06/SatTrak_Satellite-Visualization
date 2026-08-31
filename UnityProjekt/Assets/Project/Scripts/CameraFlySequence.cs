using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class CameraFlySequence : MonoBehaviour
{
    [Header("Target transform (X/Y position and rotation)")]
    public Transform uiAnchor;

    [Header("Fixed Z value for the close-up view")]
    public float fixedTargetZ = -987f;

    [Header("Travel duration per direction in seconds")]
    public float travelTime = 2.5f;

    [Header("Smoothing (0 = linear, 2-5 = soft)")]
    [Range(0f, 5f)]
    public float smoothness = 2f;

    private MainMenuCameraMovement orbitScript;
    private Vector3 startPos;
    private Quaternion startRot;

    private void Awake()
    {
        orbitScript = GetComponent<MainMenuCameraMovement>();
        startPos = transform.position;
        startRot = transform.rotation;
    }

    private Vector3 orbitResumePos;
    private Quaternion orbitResumeRot;

    public void FlyToUI()
    {

        orbitResumePos = transform.position;
        orbitResumeRot = transform.rotation;

        Vector3 uiPos = uiAnchor.position;
        uiPos.z = fixedTargetZ;
        uiPos.y = 1023f;

        StartCoroutine(Fly(transform.position, transform.rotation,
                           uiPos, uiAnchor.rotation,
                           disableOrbit: true));
    }

    public void FlyBack()
    {
        StartCoroutine(Fly(transform.position, transform.rotation,
                           orbitResumePos, orbitResumeRot,
                           disableOrbit: false));
    }

    private IEnumerator Fly(Vector3 fromPos, Quaternion fromRot,
                            Vector3 toPos, Quaternion toRot,
                            bool disableOrbit)
    {
        orbitScript.enabled = !disableOrbit;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / travelTime;

            float s = EaseInOutCubic(Mathf.Clamp01(t));

            transform.position = Vector3.Lerp(fromPos, toPos, s);
            transform.rotation = Quaternion.Slerp(fromRot, toRot, s);
            yield return null;
        }
        orbitScript.enabled = disableOrbit ? false : true;
    }

    private static float EaseInOutCubic(float x)
    {
        return (x < 0.5f)
            ? 4f * x * x * x
            : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }
}
