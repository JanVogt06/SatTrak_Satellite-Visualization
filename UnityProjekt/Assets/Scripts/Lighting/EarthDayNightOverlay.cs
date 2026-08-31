using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;

public class EarthDayNightOverlay : MonoBehaviour
{
    [Header("References")]
    public CesiumGeoreference georeference;
    public DayNightSystem dayNightSystem;

    [Header("Overlay Settings")]
    public Material overlayMaterial;
    [Range(0f, 1f)]
    public float shadowStrength = 0.9f;
    [Range(0.01f, 0.5f)]
    public float terminatorSoftness = 0.5f;

    [Header("Sphere Settings")]
    [Tooltip("Scale factor for the shadow sphere")]
    public float sphereScale = 1.05f;

    private GameObject shadowSphere;
    private Renderer sphereRenderer;

    void Start()
    {
        if (overlayMaterial == null)
        {
            Debug.LogError("EarthDayNightOverlay: no material assigned");
            enabled = false;
            return;
        }

        CreateShadowSphere();
    }

    void CreateShadowSphere()
    {

        shadowSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shadowSphere.name = "Earth Shadow Overlay";
        shadowSphere.transform.SetParent(transform);

        if (georeference != null)
        {

            var earthCenter = georeference.TransformEarthCenteredEarthFixedPositionToUnity(new double3(0, 0, 0));
            shadowSphere.transform.position = new Vector3((float)earthCenter.x, (float)earthCenter.y, (float)earthCenter.z);
        }
        else
        {
            shadowSphere.transform.position = Vector3.zero;
        }

        float earthRadiusMeters = 6371000f * sphereScale;
        shadowSphere.transform.localScale = Vector3.one * earthRadiusMeters * 2f;

        sphereRenderer = shadowSphere.GetComponent<Renderer>();

        sphereRenderer.material = overlayMaterial;
        sphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sphereRenderer.receiveShadows = false;

        Destroy(shadowSphere.GetComponent<Collider>());

        UpdateShaderProperties();
    }

    void Update()
    {
        if (dayNightSystem == null || sphereRenderer == null) return;

        if (georeference != null)
        {
            var earthCenter = georeference.TransformEarthCenteredEarthFixedPositionToUnity(new double3(0, 0, 0));
            shadowSphere.transform.position = new Vector3((float)earthCenter.x, (float)earthCenter.y, (float)earthCenter.z);
        }

        UpdateShaderProperties();
    }

    void UpdateShaderProperties()
    {
        if (sphereRenderer == null || sphereRenderer.material == null) return;

        Vector3 sunDirection = -dayNightSystem.sunLight.transform.forward;

        sphereRenderer.material.SetVector("_SunDirection", sunDirection);
        sphereRenderer.material.SetFloat("_ShadowStrength", shadowStrength);
        sphereRenderer.material.SetFloat("_TerminatorSoftness", terminatorSoftness);
    }

    public void SetShadowStrength(float strength)
    {
        shadowStrength = Mathf.Clamp01(strength);
        UpdateShaderProperties();
    }

    public void SetTerminatorSoftness(float softness)
    {
        terminatorSoftness = Mathf.Clamp(softness, 0.01f, 0.5f);
        UpdateShaderProperties();
    }

    void OnDestroy()
    {
        if (shadowSphere != null)
            Destroy(shadowSphere);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (dayNightSystem != null && dayNightSystem.sunLight != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 sunDir = -dayNightSystem.sunLight.transform.forward;

            if (shadowSphere != null)
            {

                Gizmos.DrawRay(shadowSphere.transform.position, sunDir * 10000000);

                Gizmos.color = new Color(1, 0.5f, 0, 0.5f);
                Vector3 perpendicular = Vector3.Cross(sunDir, Vector3.up).normalized;
                if (perpendicular.magnitude < 0.1f)
                    perpendicular = Vector3.Cross(sunDir, Vector3.right).normalized;

                for (int i = 0; i < 360; i += 10)
                {
                    Quaternion rotation = Quaternion.AngleAxis(i, sunDir);
                    Vector3 point = rotation * perpendicular * shadowSphere.transform.localScale.x * 0.5f;
                    Gizmos.DrawLine(
                        shadowSphere.transform.position + point * 0.99f,
                        shadowSphere.transform.position + point * 1.01f
                    );
                }
            }
        }
    }
}
