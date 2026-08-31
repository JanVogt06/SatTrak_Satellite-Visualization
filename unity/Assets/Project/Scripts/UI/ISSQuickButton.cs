using UnityEngine;
using UnityEngine.UI;
using Satellites;
using System.Linq;
using System.Collections;

public class ISSQuickButton : MonoBehaviour
{
    [Header("References")]
    public Button issButton;
    public SearchPanelController searchPanelController;

    private Satellite issSatellite;

    void Start()
    {
        if (issButton == null || searchPanelController == null)
        {
            Debug.LogError("ISS button or SearchPanelController not assigned");
            return;
        }

        StartCoroutine(WaitForISS());
    }

    IEnumerator WaitForISS()
    {

        yield return new WaitForSeconds(2f);

        var satellites = SatelliteManager.Instance.GetAllSatellites();
        Debug.Log($"Satellites found: {satellites.Count}");

        issSatellite = satellites.FirstOrDefault(s => s.IsISS);

        if (issSatellite != null)
        {
            Debug.Log($"ISS found: {issSatellite.name}");
            issButton.onClick.AddListener(OnISSButtonClick);
        }
        else
        {
            Debug.LogWarning("ISS not found, searching by name");

            var possibleISS = satellites.Where(s =>
                s.name.Contains("25544") ||
                s.name.ToUpper().Contains("ISS") ||
                s.name.Contains("ZARYA")
            ).ToList();

            foreach(var sat in possibleISS)
            {
                Debug.Log($"Possible ISS found: {sat.name}");
            }
        }
    }

    void OnISSButtonClick()
    {
        Debug.Log("ISS Button geklickt!");

        if (issSatellite != null && searchPanelController != null)
        {
            searchPanelController.OnItemSelected(issSatellite.name);
        }
    }
}
