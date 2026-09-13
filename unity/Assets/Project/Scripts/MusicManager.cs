using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Tooltip("Folder below StreamingAssets holding the tracks")]
    public string musicFolder = "music";

    [Tooltip("Track file names inside that folder")]
    public string[] musicFiles;

    public AudioSource audioSource;

    [HideInInspector] public float volume = 1f;
    [HideInInspector] public bool isMuted = false;

    private const string VolumeKey = "MusicVolume";
    private const string MuteKey = "MusicMuted";

    private float savedVolumeBeforeMute = 0.5f;
    private bool isLoading;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop = false;
        audioSource.playOnAwake = false;

        volume = PlayerPrefs.HasKey(VolumeKey) ? PlayerPrefs.GetFloat(VolumeKey) : 0.5f;
        isMuted = PlayerPrefs.GetInt(MuteKey, 0) == 1;

        savedVolumeBeforeMute = volume;
        ApplyVolume();
    }

    void Start()
    {
        PlayRandomTrack();
    }

    private void Update()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError("MusicManager: AudioSource could not be restored");
                return;
            }

            audioSource.loop = false;
            audioSource.playOnAwake = false;
            ApplyVolume();
        }

        if (!audioSource.isPlaying && !isLoading && musicFiles.Length > 0)
        {
            PlayRandomTrack();
        }

        ApplyVolume();
    }

    public void SetVolume(float value)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(value, volume)) return;

        volume = value;
        savedVolumeBeforeMute = volume;

        if (!isMuted)
            ApplyVolume();

        PlayerPrefs.SetFloat(VolumeKey, volume);
        PlayerPrefs.Save();
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        PlayerPrefs.SetInt(MuteKey, isMuted ? 1 : 0);
        PlayerPrefs.Save();

        if (isMuted)
        {
            audioSource.volume = 0f;
        }
        else
        {
            if (Mathf.Approximately(savedVolumeBeforeMute, 0f))
            {
                savedVolumeBeforeMute = 0.2f;
                volume = 0.2f;
                PlayerPrefs.SetFloat(VolumeKey, 0.2f);
                PlayerPrefs.Save();
            }

            volume = savedVolumeBeforeMute;
            ApplyVolume();
        }
    }

    public void ApplyVolume()
    {
        if (audioSource == null)
        {
            Debug.LogWarning("AudioSource is null, MusicManager was probably destroyed");
            return;
        }

        audioSource.volume = isMuted ? 0f : volume;
    }

    public void PlayRandomTrack()
    {
        if (musicFiles.Length == 0 || isLoading) return;

        StartCoroutine(LoadAndPlay(musicFiles[Random.Range(0, musicFiles.Length)]));
    }

    private IEnumerator LoadAndPlay(string fileName)
    {
        isLoading = true;

        string url = Path.Combine(Application.streamingAssetsPath, musicFolder, fileName);
        if (!url.Contains("://"))
            url = "file://" + url;

        using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"MusicManager: could not load {fileName}: {request.error}");
            isLoading = false;
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
        if (clip == null)
        {
            Debug.LogError($"MusicManager: {fileName} did not decode into a clip");
            isLoading = false;
            yield break;
        }

        clip.name = fileName;
        audioSource.clip = clip;
        audioSource.Play();

        isLoading = false;
    }
}
