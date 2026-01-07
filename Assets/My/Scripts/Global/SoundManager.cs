using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary> 오디오 클립을 로드하고 BGM 및 SFX를 재생함. </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    private readonly Dictionary<string, (AudioClip clip, float volume)> _soundLibrary = new Dictionary<string, (AudioClip, float)>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

            bgmSource.loop = true;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        LoadAllSounds().Forget();
    }

    /// <summary> 설정 로드 완료를 대기한 후 사운드를 로드함. </summary>
    private async UniTaskVoid LoadAllSounds()
    {
        try
        {
            await UniTask.WaitUntil(() => JsonLoader.Instance != null && JsonLoader.Instance.settings != null);

            var soundSettings = JsonLoader.Instance.settings.sounds;
            if (soundSettings != null)
            {
                await Initialize(soundSettings);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SoundManager] Auto Load Failed: {e}");
        }
    }

    /// <summary> Settings에 정의된 모든 사운드를 초기화함. </summary>
    private async UniTask Initialize(SoundSetting[] soundSettings)
    {
        if (soundSettings == null) return;

        foreach (var setting in soundSettings)
        {
            if (string.IsNullOrEmpty(setting.key) || string.IsNullOrEmpty(setting.clipPath)) continue;

            if (_soundLibrary.ContainsKey(setting.key)) continue;

            AudioClip clip = await LoadAudioClipFromStreamingAssets(setting.clipPath);
            if (clip != null)
            {
                clip.name = setting.key;
                _soundLibrary.Add(setting.key, (clip, setting.volume));
            }
        }
        
        Debug.Log($"[SoundManager] Initialized {_soundLibrary.Count} sounds.");
    }

    public void PlaySFX(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (_soundLibrary.TryGetValue(key, out var data))
        {
            sfxSource.PlayOneShot(data.clip, data.volume);
        }
    }

    public void PlayBGM(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            bgmSource.Stop();
            return;
        }

        if (_soundLibrary.TryGetValue(key, out var data))
        {
            if (bgmSource.clip == data.clip && bgmSource.isPlaying) return;

            bgmSource.clip = data.clip;
            bgmSource.volume = data.volume;
            bgmSource.Play();
        }
    }
    
    /// <summary> BGM을 일시 정지함. </summary>
    public void PauseBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Pause();
        }
    }

    /// <summary> BGM을 다시 재생함. </summary>
    public void ResumeBGM()
    {
        if (bgmSource != null)
        {
            bgmSource.UnPause();
        }
    }

    /// <summary> StreamingAssets에서 오디오 클립을 로드함. </summary>
    private async UniTask<AudioClip> LoadAudioClipFromStreamingAssets(string relativePath)
    {
        string path = Path.Combine(Application.streamingAssetsPath, relativePath);
        
        path = path.Replace("\\", "/");
        string fullPath = "file://" + path;

        AudioType audioType = GetAudioType(relativePath);

        using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(fullPath, audioType))
        {
            try
            {
                await uwr.SendWebRequest();

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    return DownloadHandlerAudioClip.GetContent(uwr);
                }
                else
                {
                    Debug.LogError($"[SoundManager] Failed to load: {fullPath}\nError: {uwr.error}");
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SoundManager] Load Exception: {e}");
                return null;
            }
        }
    }

    private AudioType GetAudioType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        switch (ext)
        {
            case ".mp3": return AudioType.MPEG;
            case ".wav": return AudioType.WAV;
            case ".ogg": return AudioType.OGGVORBIS;
            default: return AudioType.UNKNOWN;
        }
    }
}