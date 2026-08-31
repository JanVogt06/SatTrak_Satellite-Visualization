using System.Collections.Generic;
using UnityEngine;

public class MainMenuSatelliteSpawner : MonoBehaviour
{
    public GameObject[] satellitePrefabs;
    public int satelliteCount = 6;
    public float radius = 20f;
    public float heightRange = 5f;
    public float rotationSpeed = 30f;
    public float forwardOffset = 30f; // Abstand vor der Kamera

    private List<GameObject> satellites = new();
    private List<Vector3> rotationAxes = new();

    void Start()
    {
        SpawnSatellitesInFrontCircle();
    }

    void Update()
    {
        for (int i = 0; i < satellites.Count; i++)
        {
            var sat = satellites[i];
            if (sat != null)
            {
                // Jeder Satellit rotiert um seine eigene zufällige Achse
                sat.transform.Rotate(rotationAxes[i] * rotationSpeed * Time.deltaTime, Space.Self);
            }
        }
    }

    void SpawnSatellitesInFrontCircle()
    {
        Transform cam = Camera.main.transform;
        Vector3 center = cam.position + cam.forward * forwardOffset;

        for (int i = 0; i < satelliteCount; i++)
        {
            float angle = i * Mathf.PI * 2f / satelliteCount;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = Random.Range(-heightRange, heightRange);

            Vector3 offset = new Vector3(x, y, z);
            Vector3 position = center + offset;

            var prefab = satellitePrefabs[Random.Range(0, satellitePrefabs.Length)];
            GameObject satellite = Instantiate(prefab, position, Quaternion.identity);

            satellite.transform.LookAt(cam.position); // Optional: schaut zur Kamera

            satellites.Add(satellite);
            rotationAxes.Add(Random.onUnitSphere); // zufällige 3D-Achse speichern
        }
    }
}
