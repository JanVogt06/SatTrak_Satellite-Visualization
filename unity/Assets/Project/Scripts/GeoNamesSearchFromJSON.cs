using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using System.Collections;
using Geo;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;

[Serializable]
public class LocationEntry
{
    public string name;
    public double lat;
    public double lon;
}

public class GeoNamesSearchFromJSON : MonoBehaviour
{

    [Header("City Database")]
    [FormerlySerializedAs("geoJson")]
    public TextAsset cityDatabase;

    [Header("UI References")]
    public GameObject panel;
    public Button openButton;
    public RectTransform contentParent;
    public GameObject rowPrefab;
    public Button nextPageButton;
    public Button prevPageButton;
    public Button firstPageButton;
    public Button lastPageButton;
    public TextMeshProUGUI pageLabel;
    public TMP_InputField searchInputField;
    public TMP_Dropdown filterDropdown;

    [Header("External References")]
    public ViewModeController zoomController;
    public Georeference georeference;

    [Header("Settings")]
    public int itemsPerPage = 20;

    [Tooltip("Camera altitude in meters when flying to a city")]
    public double earthViewAltitude = 250000;

    private readonly Dictionary<string, LocationEntry> _lookup = new();

    private int _currentPage = 0;
    private int _totalPages = 1;

    public TextMeshProUGUI foundPlacesText;

    private List<LocationEntry> _nameAsc;
    private List<LocationEntry> _nameDesc;

    private enum FilterMode
    {
        All,
        Famous,
        NameAscending,
        NameDescending
    }

    private List<LocationEntry> _allLocations = new();
    private List<LocationEntry> _filtered = new();
    private List<LocationEntry> _famousList;

    private FilterMode _mode = FilterMode.All;

    private static readonly string[] _famous = {
    "London","Paris","Berlin","Madrid","Rome","Vienna","Warsaw","Prague","Budapest","Amsterdam",
    "Brussels","Athens","Stockholm","Copenhagen","Dublin","Lisbon","Oslo","Helsinki","Bucharest","Sofia",
    "Zagreb","Belgrade","Kyiv","Moscow","Jena",
    "New York City","Beijing","Tokyo","New Delhi","Cairo","Riyadh","Mexico City","Brasília",
    "Buenos Aires","Ottawa","Canberra","Pretoria","Nairobi","Jakarta","Seoul","Bangkok"
};

    private void Awake()
    {
        _allLocations = CityDatabase.Read(cityDatabase.bytes);

        foreach (var e in _allLocations)
            _lookup[e.name] = e;

        _nameAsc = _allLocations;
        _nameDesc = Enumerable.Reverse(_allLocations).ToList();

        _filtered = new List<LocationEntry>(_allLocations);
        _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)_filtered.Count / itemsPerPage));

        var famousHash = new HashSet<string>(_famous, StringComparer.OrdinalIgnoreCase);
        _famousList = _allLocations.Where(l => famousHash.Contains(l.name)).ToList();

        LocalizationSettings.SelectedLocaleChanged += _ =>
        {
            PopulateFilterDropdown();
            pageLabel.text = _filtered.Count == 0
                ? L("GameScene", "NoResults")
                : L("GameScene", "PageLabel", _currentPage + 1, _totalPages);
        };
    }

    private void Start()
    {
        panel.SetActive(false);
        openButton.onClick.AddListener(TogglePanel);

        nextPageButton.onClick.AddListener(() => ShowPage(_currentPage + 1));
        prevPageButton.onClick.AddListener(() => ShowPage(_currentPage - 1));
        firstPageButton.onClick.AddListener(() => ShowPage(0));
        lastPageButton.onClick.AddListener(() => ShowPage(_totalPages - 1));

        searchInputField.onValueChanged.AddListener(OnSearchChanged);

        PopulateFilterDropdown();
        filterDropdown.onValueChanged.AddListener(OnFilterChanged);

        ShowPage(0);
    }

    private static string L(string table, string key, params object[] args)
    {
        return args.Length == 0
            ? LocalizationSettings.StringDatabase.GetLocalizedString(table, key)
            : LocalizationSettings.StringDatabase.GetLocalizedString(table, key, args);
    }

    private void PopulateFilterDropdown()
    {
        filterDropdown.ClearOptions();
        var opts = new List<TMP_Dropdown.OptionData>
        {
            new(L("GameScene", "gs.AllCitys")),
            new(L("GameScene", "gs.FamousCity")),
            new(L("GameScene", "gs.NameAsc")),
            new(L("GameScene", "gs.NameDesc"))
        };
        filterDropdown.AddOptions(opts);
        filterDropdown.RefreshShownValue();
    }

    private void TogglePanel()
    {
        bool open = !panel.activeSelf;
        panel.SetActive(open);

        openButton.GetComponent<Image>().color =
            open ? new Color(1f, 0.7059f, 0f, 1f) : new Color(1f, 1f, 1f, 1f);

        if (open)
        {
            searchInputField.SetTextWithoutNotify(string.Empty);
            _mode = FilterMode.All;
            filterDropdown.SetValueWithoutNotify(0);

            _filtered = _allLocations;
            _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)_filtered.Count / itemsPerPage));
            UpdateFoundLabel();
            ShowPage(0);
        }
    }

    private void UpdateFoundLabel()
    {
        if (foundPlacesText != null)
            foundPlacesText.text = _filtered.Count.ToString();
    }

    private void OnFilterChanged(int index)
    {
        _mode = (FilterMode)index;
        searchInputField.SetTextWithoutNotify(string.Empty);
        ApplyModeFilter();
    }

    private void ShowPage(int index)
    {
        if (_filtered.Count == 0)
        {
            ClearRows();
            pageLabel.text = L("GameScene", "NoResults");
            prevPageButton.interactable =
            nextPageButton.interactable =
            firstPageButton.interactable =
            lastPageButton.interactable = false;
            return;
        }

        _currentPage = Mathf.Clamp(index, 0, _totalPages - 1);

        bool first = _currentPage == 0;
        bool last = _currentPage == _totalPages - 1;

        prevPageButton.interactable = !first;
        firstPageButton.interactable = !first;
        nextPageButton.interactable = !last;
        lastPageButton.interactable = !last;

        pageLabel.text = L("GameScene", "PageLabel", _currentPage + 1, _totalPages);

        ClearRows();

        int start = _currentPage * itemsPerPage;
        int end = Mathf.Min(start + itemsPerPage, _filtered.Count);

        for (int i = start; i < end; i++)
        {
            LocationEntry entry = _filtered[i];

            GameObject row = Instantiate(rowPrefab, contentParent);
            TextMeshProUGUI[] tmps = row.GetComponentsInChildren<TextMeshProUGUI>(true);

            tmps[0].text = entry.name;
            tmps[1].text = $"Lat: {entry.lat:F4}  Lon: {entry.lon:F4}";

            row.GetComponent<Button>().onClick.AddListener(() => OnItemSelected(entry.name));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent);
    }

    private void ClearRows()
    {
        foreach (Transform child in contentParent)
            Destroy(child.gameObject);
    }

    public void ResetPanel()
    {
        searchInputField.SetTextWithoutNotify(string.Empty);
        _mode = FilterMode.All;
        filterDropdown.SetValueWithoutNotify(0);

        _filtered = new List<LocationEntry>(_allLocations);
        _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)_filtered.Count / itemsPerPage));
        ShowPage(0);

        pageLabel.text = L("GameScene", "PageLabel", 1, _totalPages);
    }

    private void OnItemSelected(string placeName)
    {
        LocationEntry entry = _lookup[placeName];
        StartCoroutine(SwitchSpaceThenZoom(entry));

        panel.SetActive(false);
        openButton.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);
    }

    private IEnumerator SwitchSpaceThenZoom(LocationEntry entry)
    {

        if (Mathf.Abs(zoomController.targetCamera.fieldOfView - zoomController.spaceFov) < 0.1f)
        {
            georeference.latitude = entry.lat;
            georeference.longitude = entry.lon;
            georeference.height = 400;
            zoomController.ZoomToEarth(new double3(entry.lon, entry.lat, earthViewAltitude));
            yield break;
        }

        zoomController.ZoomToSpace();
        yield return new WaitForSeconds(2f);

        georeference.latitude = entry.lat;
        georeference.longitude = entry.lon;
        georeference.height = 400;
        zoomController.ZoomToEarth(new double3(entry.lon, entry.lat, earthViewAltitude));
    }

    private void OnSearchChanged(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ApplyModeFilter();
            return;
        }

        if (_mode != FilterMode.All)
        {
            _mode = FilterMode.All;
            filterDropdown.SetValueWithoutNotify(0);
        }

        _filtered = _allLocations
            .Where(l => l.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)_filtered.Count / itemsPerPage));
        UpdateFoundLabel();
        ShowPage(0);
    }

    private void ApplyModeFilter()
    {
        switch (_mode)
        {
            case FilterMode.All:
                _filtered = _allLocations;
                break;

            case FilterMode.Famous:
                _filtered = _famousList;
                break;

            case FilterMode.NameAscending:
                _filtered = _nameAsc;
                break;

            case FilterMode.NameDescending:
                _filtered = _nameDesc;
                break;
        }

        _totalPages = Mathf.Max(1, Mathf.CeilToInt((float)_filtered.Count / itemsPerPage));
        UpdateFoundLabel();
        ShowPage(0);
    }

    public void CloseTheWindow()
    {
        panel.SetActive(false);

        openButton.GetComponent<Image>().color = new Color(1f, 1f, 1f, 1f);

        EventSystem.current.SetSelectedGameObject(null);
    }
}
