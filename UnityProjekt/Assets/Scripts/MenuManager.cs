using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public GameObject mainMenu;
    public GameObject settingsMenu;
    public GameObject creditsMenu;

    public Slider volumeSlider;
    public TextMeshProUGUI volumeText;
    public Button muteButton;
    public Image muteButtonImage;
    public Sprite muteOnSprite;
    public Sprite muteOffSprite;

    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;

    private Resolution[] resolutions;
    private List<string> resolutionOptions = new();
    private int currentResolutionIndex = 0;

    private MusicManager musicManager => MusicManager.Instance;

    public Toggle showFpsToggle;
    private const string PrefKey = "ShowFPS";

    public TMP_Dropdown languageDropdown;
    private const string LocalePrefKey = "LocaleIndex";

    private string tableName = "MainMenuTable";
    private string englishKey = "EnglishLbl";
    private string germanKey = "GermanLbl";

    void Start()
    {
        showFpsToggle.isOn = PlayerPrefs.GetInt(PrefKey, 0) == 1;

        showFpsToggle.onValueChanged.AddListener(OnToggleChanged);

        InitializeLanguageDropdown();

        InitializeQualityDropdown();
        InitializeResolutionDropdown();

        UpdateAudioUI();
        BindEvents();
        OpenMainMenu();
    }

    void Awake()
    {
        int savedIndex = PlayerPrefs.GetInt(LocalePrefKey, 0);
        ApplyLocale(savedIndex);

        UpdateLanguageDropdown();
        UpdateQualityDropdown();

        LocalizationSettings.SelectedLocaleChanged += _ =>
        {
            UpdateLanguageDropdown();
            UpdateQualityDropdown();
        };
    }

    void UpdateQualityDropdown()
    {
        var db = LocalizationSettings.StringDatabase;
        var opts = new List<TMP_Dropdown.OptionData>();

        string[] keys = { "QL_Performance", "QL_Balanced", "QL_HighQuality" };

        for (int i = 0; i < keys.Length; i++)
        {
            string label = db.GetLocalizedString("MainMenuTable", keys[i]);
            if (string.IsNullOrEmpty(label)) label = QualitySettings.names[i];
            opts.Add(new TMP_Dropdown.OptionData(label));
        }

        qualityDropdown.options = opts;
        qualityDropdown.RefreshShownValue();
    }

    void UpdateLanguageDropdown()
    {
        var db = LocalizationSettings.StringDatabase;
        var opts = new List<TMP_Dropdown.OptionData>
    {
        new(db.GetLocalizedString(tableName, englishKey)),
        new(db.GetLocalizedString(tableName, germanKey))
    };

        languageDropdown.options = opts;
        languageDropdown.RefreshShownValue();

        int current = LocalizationSettings.AvailableLocales.Locales
                       .IndexOf(LocalizationSettings.SelectedLocale);
        languageDropdown.SetValueWithoutNotify(current);
    }

    void InitializeLanguageDropdown()
    {
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    private void OnLanguageChanged(int index)
    {
        ApplyLocale(index);
        PlayerPrefs.SetInt(LocalePrefKey, index);
        PlayerPrefs.Save();
    }

    private static void ApplyLocale(int index)
    {
        IList<Locale> locales = LocalizationSettings.AvailableLocales.Locales;

        if (index >= 0 && index < locales.Count)
            LocalizationSettings.SelectedLocale = locales[index];
    }

    void BindEvents()
    {
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        muteButton.onClick.AddListener(OnMuteClicked);
    }

    public void CloseGame()
    {
        Application.Quit();
    }

    private static void OnToggleChanged(bool value)
    {
        PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void OnVolumeChanged(float value)
    {
        musicManager.isMuted = false;
        musicManager.SetVolume(value);
        UpdateAudioUI();
    }

    public void OnMuteClicked()
    {
        musicManager.ToggleMute();
        UpdateAudioUI();
    }

    public void UpdateAudioUI()
    {
        float vol = musicManager.isMuted ? 0f : musicManager.volume;

        volumeSlider.onValueChanged.RemoveAllListeners();
        volumeSlider.value = vol;
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        volumeText.text = Mathf.RoundToInt(vol * 100f) + "%";

        bool muted = musicManager.isMuted;

        muteButtonImage.sprite = muted ? muteOffSprite : muteOnSprite;
        muteButtonImage.color = muted ? Color.red : Color.white;

        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    public void OpenMainMenu()
    {
        mainMenu.SetActive(true);
        settingsMenu.SetActive(false);
        creditsMenu.SetActive(false);
    }

    public void OpenMainMenuFromCredits()
    {
        settingsMenu.SetActive(false);
        creditsMenu.SetActive(false);

        StartCoroutine(OpenMainMenuDelay());
    }

    public void OpenSettingsMenu()
    {
        mainMenu.SetActive(false);
        creditsMenu.SetActive(false);
        settingsMenu.SetActive(true);
        UpdateAudioUI();
    }

    public void OpenCreditsMenu()
    {
        mainMenu.SetActive(false);
        settingsMenu.SetActive(false);
        StartCoroutine(OpenCreditsDelay());
    }

    public IEnumerator OpenCreditsDelay()
    {
        yield return new WaitForSeconds(4f);
        creditsMenu.SetActive(true);
    }

    public IEnumerator OpenMainMenuDelay()
    {
        yield return new WaitForSeconds(4f);
        mainMenu.SetActive(true);
    }

    void InitializeQualityDropdown()
    {
        qualityDropdown.onValueChanged.AddListener(SetQualityLevel);
    }

    void SetQualityLevel(int index)
    {
        QualitySettings.SetQualityLevel(index, true);
    }

    void InitializeResolutionDropdown()
    {
        resolutionDropdown.ClearOptions();
        resolutions = Screen.resolutions;
        resolutionOptions.Clear();

        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution res = resolutions[i];
            string option = $"{res.width}x{res.height} @ {Mathf.RoundToInt((float)res.refreshRateRatio.value)}Hz";
            resolutionOptions.Add(option);

            if (res.width == Screen.currentResolution.width &&
                res.height == Screen.currentResolution.height &&
                Mathf.Approximately((float)res.refreshRateRatio.value, (float)Screen.currentResolution.refreshRateRatio.value))
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(resolutionOptions);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }

    void SetResolution(int index)
    {
        Resolution res = resolutions[index];
        Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow, res.refreshRateRatio);
    }

    public void LoadGame()
    {
        SceneSwitcher.Instance.SwitchScene("GameScene");
    }
}
