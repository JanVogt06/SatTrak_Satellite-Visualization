using Satellites;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SatelliteShowHide : MonoBehaviour
{
    public Toggle showHide;
    public GameObject satelliteParent;

    public SearchPanelController searchPanelController;

    private void Start()
    {
        showHide.isOn = true;
        satelliteParent.SetActive(true);
    }

    public void ShowHideSatellites()
    {
        bool state = showHide.isOn;

        if (state == true)
        {
            searchPanelController.openButton.interactable = true;
            searchPanelController.openButton.GetComponent<Image>().sprite = searchPanelController.normalSatButton;
        }
        else
        {
            searchPanelController.openButton.interactable = false;
            searchPanelController.openButton.GetComponent<Image>().sprite = searchPanelController.disabledSatButton;
        }

        satelliteParent.SetActive(state);

        SatelliteManager.Instance.satellitesActive = state;
    }

}
