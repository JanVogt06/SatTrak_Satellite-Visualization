using System;
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

    private const float OBLIQUITY = 23.44f;
    private const float DAYS_PER_YEAR = 365.25f;

    void Start()
    {

        if (sunLight == null)
            sunLight = GameObject.Find("Directional Light")?.GetComponent<Light>();

        if (timeSlider == null)
            timeSlider = FindObjectOfType<TimeSlider.TimeSlider>();

        if (sunLight == null || timeSlider == null)
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

        Vector3 sunDirection = CalculateSunDirection(timeSlider.CurrentSimulatedTime);

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

    Vector3 CalculateSunDirection(DateTime currentTime)
    {

        DateTime equinox = new DateTime(currentTime.Year, 3, 21);
        double daysSinceEquinox = (currentTime - equinox).TotalDays;

        double eclipticLongitude = (360.0 / DAYS_PER_YEAR) * daysSinceEquinox;
        double eclipticLongitudeRad = eclipticLongitude * Mathf.Deg2Rad;

        double declination = OBLIQUITY * Math.Sin(eclipticLongitudeRad);
        double declinationRad = declination * Mathf.Deg2Rad;

        double hourAngle = (currentTime.TimeOfDay.TotalHours - 12.0) * 15.0;
        double hourAngleRad = hourAngle * Mathf.Deg2Rad;

        float x = (float)(Math.Cos(declinationRad) * Math.Sin(hourAngleRad));
        float y = (float)(Math.Sin(declinationRad));
        float z = (float)(Math.Cos(declinationRad) * Math.Cos(hourAngleRad));

        return new Vector3(x, y, z).normalized;
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

        DateTime current = timeSlider.CurrentSimulatedTime;
        Debug.Log($"Time: {current:yyyy-MM-dd HH:mm:ss}");
        Debug.Log($"Sun direction: {sunDirection}");
        Debug.Log($"Sun elevation: {Vector3.Dot(sunDirection, Vector3.up):F2}");
    }

    public float GetLocalSunElevation(double latitude, double longitude, DateTime time)
    {
        Vector3 sunDir = CalculateSunDirection(time);

        float latRad = (float)(latitude * Mathf.Deg2Rad);
        float lonRad = (float)(longitude * Mathf.Deg2Rad);

        Vector3 locationVector = new Vector3(
            Mathf.Cos(latRad) * Mathf.Cos(lonRad),
            Mathf.Sin(latRad),
            Mathf.Cos(latRad) * Mathf.Sin(lonRad)
        );

        float elevation = Vector3.Dot(locationVector, sunDir);
        return Mathf.Asin(elevation) * Mathf.Rad2Deg;
    }

    public bool IsDay(double latitude, double longitude, DateTime time)
    {
        return GetLocalSunElevation(latitude, longitude, time) > -6f;
    }
}
