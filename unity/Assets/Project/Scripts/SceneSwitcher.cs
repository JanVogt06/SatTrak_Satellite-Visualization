using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneSwitcher : MonoBehaviour
{
    public static SceneSwitcher Instance;

    [SerializeField] private Slider loadingSlider;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private GameObject loadingScreenGameObject;
    private const float smoothSpeed = 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void SwitchScene(string sceneName)
    {
        loadingScreenGameObject.SetActive(true);
        loadingSlider.value = 0.0f;
        if (!string.IsNullOrEmpty(sceneName))
        {
            StartCoroutine(SwitchSceneAsync(sceneName));
        }
        else
        {
            Debug.LogWarning("No scene name given");
        }
    }

    IEnumerator SwitchSceneAsync(string sceneName)
    {
        var asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        float displayedProgress = 0f;
        while (!asyncLoad.isDone)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, asyncLoad.progress, smoothSpeed * Time.deltaTime);

            UpdateUI(displayedProgress);
            yield return null;
        }

        while (displayedProgress < 1f)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, 1f, smoothSpeed * Time.deltaTime);
            UpdateUI(displayedProgress);
            yield return null;
        }

        yield return new WaitForSeconds(2.3f);
        loadingScreenGameObject.SetActive(false);
    }

    private void UpdateUI(float progress)
    {
        loadingSlider.value = progress;
        progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
    }

    public void EndProgramm()
    {
        Application.Quit();
    }
}
