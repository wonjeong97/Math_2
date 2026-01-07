using System;
using System.IO;
using UnityEngine;

/// <summary> JSON 파일을 읽어와 데이터 객체(Settings 등)로 변환하는 로더 클래스. </summary>
public class JsonLoader : MonoBehaviour
{
    [NonSerialized] public Settings settings;

    private static JsonLoader _instance;

    public static JsonLoader Instance
    {
        get
        {
            if (ReferenceEquals(_instance, null))
            {
                _instance = FindFirstObjectByType<JsonLoader>()
                            ?? new GameObject("JsonLoader").AddComponent<JsonLoader>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 전역 설정 로드
        settings = LoadJsonData<Settings>(GameConstants.Path.JsonSetting);
    }

    private void Start()
    {
        if (settings == null)
        {
            Debug.LogError($"[JsonLoader] Settings.json Load Failed.");
        }
    }

    /// <summary> StreamingAssets 폴더에서 JSON 파일을 읽어와 타입 T로 파싱하여 반환합니다. </summary>
    public T LoadJsonData<T>(string fileName)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName).Replace("\\", "/");

        if (!File.Exists(filePath))
        {
            Debug.LogWarning("[JsonLoader] File not found: " + filePath);
            return default;
        }

        try 
        {
            string json = File.ReadAllText(filePath);
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[JsonLoader] Failed to parse {fileName}: {e.Message}");
            return default;
        }
    }
}