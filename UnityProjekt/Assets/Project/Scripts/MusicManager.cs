using System.Collections.Generic;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    public List<AudioClip> musicClips;
    public AudioSource audioSource;

    [HideInInspector] public float volume = 1f;
    [HideInInspector] public bool isMuted = false;

    private const string VolumeKey = "MusicVolume";
    private const string MuteKey = "MusicMuted";

    private float savedVolumeBeforeMute = 0.5f;

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

        if (!audioSource.isPlaying && musicClips.Count > 0)
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
        if (musicClips.Count == 0) return;

        int index = Random.Range(0, musicClips.Count);
        audioSource.clip = musicClips[index];
        audioSource.Play();
    }
}
