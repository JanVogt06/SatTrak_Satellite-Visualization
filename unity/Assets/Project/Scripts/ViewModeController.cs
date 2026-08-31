using UnityEngine;
using System.Collections;
using Geo;
using Unity.Mathematics;
using UnityEngine.UI;
using Satellites;
using UnityEngine.Experimental.GlobalIllumination;
using TMPro;

public class ViewModeController : MonoBehaviour
{
    public GlobeAnchor globeAnchor;

    public double3 spaceView = new double3(11.666985, 51.217959, 300);

    public Vector3 spaceRotation = new Vector3(45f, 0f, 0f);
    public Vector3 earthRotation = new Vector3(25f, 0f, 0f);

    [Header("FOV Transition")]
    public Camera targetCamera;
    public float earthFov = 60f;
    public float spaceFov = 80f;
    public float fovTransitionDuration = 2.3f;
    public GlobeRotationController orbitController;

    private Coroutine zoomRoutine;

    public Button spaceButton;

    public FreeFlyCamera freeFlyCameraScript;

    public Georeference georeference;

    public SearchPanelController searchPanelController;

    public Light directionalLight;

    [Header("Screen Fade")]
    public Image fadePanel;

    private bool insideSat;

    public Slider zoomSlider;

    [Header("Day/Night System")]
    public bool useDayNightSystem = true;

    private DayNightSystem dayNightSystem;

    public float nearEarth = 1f;
    public float nearSpace = 100f;

    public Sprite spaceButtonNormal;
    public Sprite spaceButtonDisabled;

    private void Start()
    {
        zoomSlider.gameObject.SetActive(false);

        insideSat = false;

        dayNightSystem = FindObjectOfType<DayNightSystem>();

        if (!useDayNightSystem || dayNightSystem == null)
        {
            directionalLight.transform.eulerAngles = new Vector3(45f, -30f, 0f);
            directionalLight.intensity = 1.3f;
        }

        spaceButton.interactable = false;
        spaceButton.GetComponent<Image>().sprite = spaceButtonDisabled;
        ZoomToSpace();
    }

    private void SetLightIntensity(float intensity)
    {
        if (!useDayNightSystem || dayNightSystem == null)
        {
            directionalLight.intensity = intensity;
        }
    }

    private void ApplyNearClip(bool space) =>
    targetCamera.nearClipPlane = space ? nearSpace : nearEarth;

    private void SetLightRotation(Quaternion rotation)
    {
        if (!useDayNightSystem || dayNightSystem == null)
        {
            directionalLight.transform.rotation = rotation;
        }
    }

    public void ZoomToEarth(double3 earthView)
    {
        if (zoomRoutine != null) StopCoroutine(zoomRoutine);
        zoomRoutine = null;
        zoomRoutine = StartCoroutine(ZoomToPosition(earthView, Quaternion.Euler(earthRotation), false, 2.3f));
        StartCoroutine(AnimateFOV(spaceFov, earthFov));

        freeFlyCameraScript.cameraModeAllowed = true;
        freeFlyCameraScript.UpdateModeUI();
    }

    public void SnapToSatellit(Satellite view)
    {
        if (zoomRoutine != null) StopCoroutine(zoomRoutine);

        zoomRoutine = StartCoroutine(ZoomToPositionSatellite(view, Quaternion.Euler(earthRotation), false));

        targetCamera.fieldOfView = 60f;
    }

    public void ZoomToSpace()
    {
        ApplyNearClip(true);

        freeFlyCameraScript.cameraModeAllowed = false;
        freeFlyCameraScript.UpdateModeUI();

        spaceButton.interactable = false;
        spaceButton.GetComponent<Image>().sprite = spaceButtonDisabled;

        if (insideSat)
        {
            StartCoroutine(FadeToBlack());
            StartCoroutine(BlackSpace());
        }
        else
        {
            zoomSlider.gameObject.SetActive(false);
            SetLightIntensity(1.3f);
            if (zoomRoutine != null) StopCoroutine(zoomRoutine);
            zoomRoutine = StartCoroutine(ZoomToPosition(spaceView, Quaternion.Euler(spaceRotation), true, 2.3f));
            georeference.latitude = 51.21796;
            georeference.longitude = 11.66699;
            georeference.height = 400;
            StartCoroutine(AnimateFOV(earthFov, spaceFov));
        }
    }

    public IEnumerator BlackSpace()
    {
        yield return new WaitForSeconds(1.2f);

        zoomSlider.gameObject.SetActive(false);

        SetLightIntensity(1.3f);

        searchPanelController.StopTracking();

        spaceButton.interactable = false;
        spaceButton.GetComponent<Image>().sprite = spaceButtonDisabled;
        if (zoomRoutine != null) StopCoroutine(zoomRoutine);
        zoomRoutine = StartCoroutine(ZoomToPosition(spaceView, Quaternion.Euler(spaceRotation), true, 0.5f));
        georeference.latitude = 51.21796;
        georeference.longitude = 11.66699;
        georeference.height = 400;
        StartCoroutine(AnimateFOV(earthFov, spaceFov));

        yield return new WaitForSeconds(2f);
        StartCoroutine(FadeFromBlack());

        insideSat = false;
    }

    public IEnumerator ZoomToPositionSatellite(Satellite sat, Quaternion targetRotation, bool space)
    {
        yield return new WaitForSeconds(0.5f);

        if (sat == null) yield break;

        double3 startLLH = globeAnchor.longitudeLatitudeHeight;
        Quaternion startRotation = transform.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0;
            float smoothedT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 satWorldPos = sat.transform.position;
            double3 targetLLH = new double3(satWorldPos.x, satWorldPos.y, satWorldPos.z);

            double3 currentLLH = math.lerp(startLLH, targetLLH, smoothedT);
            globeAnchor.longitudeLatitudeHeight = currentLLH;

            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothedT);

            yield return null;
        }

        Vector3 finalPos = sat.transform.position;
        globeAnchor.longitudeLatitudeHeight = new double3(finalPos.x, finalPos.y, finalPos.z);
        transform.rotation = targetRotation;

        freeFlyCameraScript.SyncInitTransform();

        if (orbitController != null)
        {
            orbitController.InitializeOrbit();
        }

        SetLightIntensity(1.5f);
        insideSat = true;

        yield return new WaitForSeconds(1f);

        zoomSlider.gameObject.SetActive(true);

        searchPanelController.ResetZoomSlider();

        yield return FadeFromBlack();
        ApplyNearClip(true);

        if (!space)
        {
            StartCoroutine(SpaceButton(space));
        }

        SetCameraModeAbility(false);
    }

    public IEnumerator SpaceButton(bool space)
    {
        yield return new WaitForSeconds(3f);

        spaceButton.interactable = true;
        spaceButton.GetComponent<Image>().sprite = spaceButtonNormal;
    }

    void LateUpdate()
    {
        if (insideSat == true)
        {
            SetLightRotation(targetCamera.transform.rotation);
        }
    }

    IEnumerator ZoomToPosition(double3 targetLLH, Quaternion targetRotation, bool space, float zoomDuration)
    {
        double3 startLLH = globeAnchor.longitudeLatitudeHeight;
        Quaternion startRotation = transform.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / zoomDuration;
            float smoothedT = Mathf.SmoothStep(0f, 1f, t);

            double3 currentLLH = math.lerp(startLLH, targetLLH, smoothedT);
            globeAnchor.longitudeLatitudeHeight = currentLLH;

            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothedT);

            yield return null;
        }

        globeAnchor.longitudeLatitudeHeight = targetLLH;
        transform.rotation = targetRotation;

        freeFlyCameraScript.SyncInitTransform();

        if (this.orbitController != null)
        {
            this.orbitController.InitializeOrbit();
        }

        if (!space)
        {
            spaceButton.interactable = true;
            spaceButton.GetComponent<Image>().sprite = spaceButtonNormal;
            insideSat = false;
            SetLightIntensity(1.3f);
            SetLightRotation(Quaternion.Euler(-90f, 0f, 0f));
        }
        else
        {
            insideSat = false;
            SetLightIntensity(1.3f);
            SetLightRotation(Quaternion.Euler(90f, 0f, 0f));
        }

        ApplyNearClip(space);

        SetCameraModeAbility(!space);
    }

    IEnumerator AnimateFOV(float from, float to)
    {
        if (targetCamera == null)
            yield break;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / fovTransitionDuration;
            float eased = Mathf.SmoothStep(0, 1, t);
            targetCamera.fieldOfView = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        targetCamera.fieldOfView = to;
    }

    public IEnumerator FadeToBlack()
    {
        if (fadePanel == null) yield break;
        float t = 0f;
        Color c = fadePanel.color;
        while (t < 1f)
        {
            t += Time.deltaTime / 1f;
            c.a = Mathf.Lerp(0f, 1f, t);
            fadePanel.color = c;
            yield return null;
        }
        c.a = 1f;
        fadePanel.color = c;
    }

    public IEnumerator FadeFromBlack()
    {
        if (fadePanel == null) yield break;
        float t = 0f;
        Color c = fadePanel.color;
        while (t < 1f)
        {
            t += Time.deltaTime / 2f;
            c.a = Mathf.Lerp(1f, 0f, t);
            fadePanel.color = c;
            yield return null;
        }
        c.a = 0f;
        fadePanel.color = c;
    }

    private void SetCameraModeAbility(bool allowed)
    {
        freeFlyCameraScript.cameraModeAllowed = allowed;
        if (!allowed) freeFlyCameraScript.ForceInspectorMode();
    }

}
