using System;
using Geo;
using TimeExtensions = Satellites.SGP.Util.TimeExtensions;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.UI;

public class DayNightSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Directional light acting as the sun")]
    public Light sunLight;

    [Tooltip("TimeSlider providing the current time")]
    public TimeSlider.TimeSlider timeSlider;

    [Tooltip("Georeference that maps earth fixed directions into the scene")]
    public Georeference georeference;

    [Tooltip("Optional earth material for shader effects")]
    public Material earthMaterial;

    [Header("Sun Settings")]
    [Tooltip("Sunlight intensity")]
    public float sunIntensity = 1.3f;

    [Tooltip("Sunlight color")]
    public Color sunColor = new Color(1f, 0.95f, 0.8f);

    [Header("Ambient Settings")]
    [Tooltip("Ambient light during the day")]
    public Color dayAmbientColor = new Color(0.5f, 0.6f, 0.7f);

    [Tooltip("Ambient light during the night")]
    public Color nightAmbientColor = new Color(0.05f, 0.05f, 0.1f);

    [Header("Visual Effects")]
    [Tooltip("Show a visual terminator (day/night boundary)")]
    public bool showTerminator = true;

    [Tooltip("GameObject for the terminator effect")]
    public GameObject terminatorPlane;

    [Header("Debug")]
    public bool showDebugInfo = false;

    void Start()
    {

        if (sunLight == null)
            sunLight = GameObject.Find("Directional Light")?.GetComponent<Light>();

        if (timeSlider == null)
            timeSlider = FindObjectOfType<TimeSlider.TimeSlider>();

        if (georeference == null)
            georeference = FindObjectOfType<Georeference>();

        if (sunLight == null || timeSlider == null || georeference == null)
        {
            Debug.LogError("DayNightSystem: Fehlende References!");
            enabled = false;
            return;
        }

        if (showTerminator && terminatorPlane == null)
            CreateTerminatorPlane();
    }

    void Update()
    {
        if (timeSlider == null) return;

        var sunEcef = CalculateSunDirectionEcef(timeSlider.CurrentSimulatedTimeUtc);
        var sunLocal = math.normalize(georeference.TransformEarthCenteredEarthFixedDirectionToUnity(sunEcef));
        Vector3 sunDirection = new Vector3((float)sunLocal.x, (float)sunLocal.y, (float)sunLocal.z);

        sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection);

        sunLight.intensity = sunIntensity;
        sunLight.color = sunColor;

        UpdateAmbientLighting(sunDirection);

        if (earthMaterial != null)
        {
            earthMaterial.SetVector("_SunDirection", sunDirection);
            earthMaterial.SetFloat("_DayNightBlend", 0.1f);
        }

        if (showTerminator && terminatorPlane != null)
            UpdateTerminator(sunDirection);

        if (showDebugInfo)
            ShowDebugInfo(sunDirection);
    }

    static double3 CalculateSunDirectionEcef(DateTime utc)
    {
        double n = TimeExtensions.ToJulian(utc) - 2451545.0;

        double meanLongitude = math.radians(280.460 + 0.9856474 * n);
        double meanAnomaly = math.radians(357.528 + 0.9856003 * n);
        double eclipticLongitude = meanLongitude
                                   + math.radians(1.915) * math.sin(meanAnomaly)
                                   + math.radians(0.020) * math.sin(2.0 * meanAnomaly);
        double obliquity = math.radians(23.439 - 0.0000004 * n);

        var eci = new double3(
            math.cos(eclipticLongitude),
            math.cos(obliquity) * math.sin(eclipticLongitude),
            math.sin(obliquity) * math.sin(eclipticLongitude));

        math.sincos(TimeExtensions.ToGreenwichSiderealTime(utc), out var sinTheta, out var cosTheta);
        return new double3(
            cosTheta * eci.x + sinTheta * eci.y,
            -sinTheta * eci.x + cosTheta * eci.y,
            eci.z);
    }

    void UpdateAmbientLighting(Vector3 sunDirection)
    {

        float sunHeight = Vector3.Dot(sunDirection, Vector3.up);

        float dayAmount = Mathf.Clamp01((sunHeight + 0.2f) / 0.4f);

        RenderSettings.ambientLight = Color.Lerp(nightAmbientColor, dayAmbientColor, dayAmount);

        RenderSettings.fogColor = Color.Lerp(
            nightAmbientColor * 0.5f,
            dayAmbientColor * 0.8f,
            dayAmount
        );
    }

    void CreateTerminatorPlane()
    {

        terminatorPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        terminatorPlane.name = "Terminator";

        terminatorPlane.transform.localScale = new Vector3(15000000, 15000000, 1);

        Material terminatorMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        terminatorMat.color = new Color(0, 0, 0, 0.3f);
        terminatorMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        terminatorMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        terminatorMat.SetInt("_ZWrite", 0);
        terminatorMat.renderQueue = 3000;

        terminatorPlane.GetComponent<Renderer>().material = terminatorMat;

        terminatorPlane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Destroy(terminatorPlane.GetComponent<Collider>());
    }

    void UpdateTerminator(Vector3 sunDirection)
    {
        if (terminatorPlane == null) return;

        terminatorPlane.transform.position = Vector3.zero;

        terminatorPlane.transform.rotation = Quaternion.LookRotation(sunDirection, Vector3.up);

        terminatorPlane.transform.position += sunDirection * 1000;
    }

    void ShowDebugInfo(Vector3 sunDirection)
    {

        Debug.DrawRay(Vector3.zero, sunDirection * 10000000, Color.yellow);

        DateTime current = timeSlider.CurrentSimulatedTimeUtc;
        Debug.Log($"Time: {current:yyyy-MM-dd HH:mm:ss} UTC");
        Debug.Log($"Sun direction: {sunDirection}");
        Debug.Log($"Sun elevation: {Vector3.Dot(sunDirection, Vector3.up):F2}");
    }

    public float GetLocalSunElevation(double latitude, double longitude, DateTime time)
    {
        var sunEcef = CalculateSunDirectionEcef(time.ToUniversalTime());

        math.sincos(math.radians(latitude), out var sinLat, out var cosLat);
        math.sincos(math.radians(longitude), out var sinLon, out var cosLon);
        var up = new double3(cosLat * cosLon, cosLat * sinLon, sinLat);

        return (float)math.degrees(math.asin(math.dot(up, sunEcef)));
    }

    public bool IsDay(double latitude, double longitude, DateTime time)
    {
        return GetLocalSunElevation(latitude, longitude, time) > -6f;
    }
}
