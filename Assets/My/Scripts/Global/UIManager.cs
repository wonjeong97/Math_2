using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary> JSON 설정 데이터를 기반으로 UI 오브젝트(Text, Image, Button)를 동적으로 설정하고 관리하는 매니저. </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance; // 싱글톤

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>  텍스트 오브젝트 설정 (폰트, 내용, 색상, 위치 등). </summary>
    public async UniTask SetTextObj(GameObject textObj, TextSetting textSetting, string overrideText = null, CancellationToken token = default)
    {
        if (!textObj || textSetting == null)
        {
            Debug.LogError("[UIManager] SetTextObj => textObj or textSetting is null");
            return;
        }

        if (textObj.TryGetComponent(out TextMeshProUGUI tmp) && textObj.TryGetComponent(out RectTransform rt))
        {
            string text = string.IsNullOrEmpty(overrideText) ? textSetting.text : overrideText;

            // 폰트 로드 및 그라데이션 적용
            await ApplyFontAsync(tmp, textSetting, text, token);
            ApplyRect(rt, size: null, anchoredPos: new Vector2(textSetting.position.x, -textSetting.position.y), rotation: textSetting.rotation);
        }
    }

    /// <summary>  이미지 오브젝트 설정 (스트리밍 에셋 로드, 타입, 위치 등). </summary>
    public void SetImageObj(GameObject imageObj, ImageSetting imageSetting)
    {
        if (!imageObj || imageSetting == null)
        {
            Debug.LogError("[UIManager] SetImageObj => imageObj or imageSetting is null");
            return;
        }

        if (imageObj.TryGetComponent(out Image img) && imageObj.TryGetComponent(out RectTransform rt))
        {
            // 이미지 로드 및 스프라이트 생성
            Texture2D tex = LoadTextureFromStreamingAssets(imageSetting.sourceImage);
            if (tex != null)
            {
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                img.color = imageSetting.color;
                img.type = (Image.Type)imageSetting.type;
            }

            ApplyRect(rt, size: imageSetting.size, anchoredPos: new Vector2(imageSetting.position.x, -imageSetting.position.y), rotation: imageSetting.rotation, scale: imageSetting.scale);
        }
    }

    /// <summary> 버튼 오브젝트 설정 (배경 이미지, 텍스트, 위치 등). </summary>
    public async UniTask SetButtonObj(GameObject buttonObj, ButtonSetting buttonSetting, CancellationToken token = default, string overrideText = null)
    {
        if (!buttonObj || buttonSetting == null)
        {
            Debug.LogError("[UIManager] SetButtonObj-> buttonObj or buttonSetting is null");
            return;
        }

        if (!buttonObj.TryGetComponent(out RectTransform buttonRt))
        {
            Debug.LogError("[UIManager] SetButtonObj-> Button GameObject has no RectTransform");
            return;
        }

        // 버튼 위치/크기 설정
        ApplyRect(buttonRt, size: buttonSetting.size, anchoredPos: new Vector2(buttonSetting.position.x, -buttonSetting.position.y), rotation: buttonSetting.rotation, scale: buttonSetting.scale);

        // 버튼 배경 이미지 설정
        if (!buttonObj.TryGetComponent(out Image bgImage))
        {
            Debug.LogError("[UIManager] SetButtonObj-> Button GameObject has no Image");
            return;
        }

        if (buttonSetting.buttonBackgroundImage != null)
        {
            Texture2D tex = LoadTextureFromStreamingAssets(buttonSetting.buttonBackgroundImage.sourceImage);
            if (tex != null)
            {
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                bgImage.sprite = sprite;
            }

            bgImage.color = buttonSetting.buttonBackgroundImage.color;
            bgImage.type = (Image.Type)buttonSetting.buttonBackgroundImage.type;
        }

        // 버튼 텍스트 설정 (자식 컴포넌트)
        if (buttonSetting.buttonText != null)
        {
            TextMeshProUGUI tmp = buttonObj.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) await SetTextObj(tmp.gameObject, buttonSetting.buttonText, overrideText, token);
        }
    }

    #region UICreator (내부 로직)

    private readonly List<GameObject> _instances = new List<GameObject>();
    private readonly Dictionary<string, AsyncOperationHandle> _assetCache = new Dictionary<string, AsyncOperationHandle>();

    /// <summary>Addressables 에셋 로드를 캐시해 중복 로드 방지.</summary>
    private async UniTask<T> LoadAssetWithCacheAsync<T>(string key, CancellationToken token) where T : Object
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (_assetCache.TryGetValue(key, out AsyncOperationHandle existing))
        {
            return existing.IsValid() ? (T)existing.Result : null;
        }

        token.ThrowIfCancellationRequested();

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
        T asset = await AwaitWithCancellation(handle, token);

        _assetCache[key] = handle;
        return asset;
    }

    /// <summary>폰트 키를 FontMap 기준으로 해석해 실제 Addressable 키 반환.</summary>
    private static string ResolveFontKey(string key)
    {
        Settings settings = JsonLoader.Instance != null ? JsonLoader.Instance.settings : null;
        FontMaps fontMap = settings?.fontMap;
        if (fontMap == null || string.IsNullOrEmpty(key)) return key;

        FieldInfo field = typeof(FontMaps).GetField(key);
        if (field != null)
        {
            string mapped = field.GetValue(fontMap) as string;
            return string.IsNullOrEmpty(mapped) ? key : mapped;
        }

        return key;
    }

    /// <summary> 폰트 로드 및 텍스트 속성(그라데이션 포함) 적용. </summary>
    private async UniTask ApplyFontAsync(TextMeshProUGUI uiText, TextSetting setting, string textValue, CancellationToken token)
    {
        if (!uiText || setting == null) return;

        string mapped = ResolveFontKey(setting.fontName);
        TMP_FontAsset font = await LoadAssetWithCacheAsync<TMP_FontAsset>(mapped, token);
        if (font == null) return;
        token.ThrowIfCancellationRequested();

        uiText.font = font;
        uiText.fontSize = setting.fontSize;
        uiText.alignment = setting.alignment;
        uiText.text = textValue;

        if (setting.useGlobalGradient)
        {
            // 전체 그라데이션 컴포넌트 사용
            var gradientEffect = GetOrAdd<TextGlobalGradient>(uiText.gameObject);
            gradientEffect.SetGradient(
                setting.gradientTopLeft, 
                setting.gradientTopRight, 
                setting.gradientBottomLeft, 
                setting.gradientBottomRight
            );
            gradientEffect.enabled = true;

            uiText.enableVertexGradient = false;
            uiText.color = Color.white;
        }
        else
        {
            if (uiText.TryGetComponent<TextGlobalGradient>(out var effect))
            {
                effect.enabled = false;
            }

            // 기본 TMP 그라데이션 사용
            if (setting.useGradient)
            {
                uiText.enableVertexGradient = true;
                uiText.colorGradient = new VertexGradient(
                    setting.gradientTopLeft, setting.gradientTopRight, 
                    setting.gradientBottomLeft, setting.gradientBottomRight
                );
                uiText.color = Color.white;
            }
            else
            {
                uiText.enableVertexGradient = false;
                uiText.color = setting.fontColor;
            }
        }
    }
    
    #endregion

    #region UIUtility
    
    /// <summary> 외부에서 경로만으로 스프라이트 로드. </summary>
    public Sprite LoadSprite(string relativePath)
    {
        Texture2D tex = LoadTextureFromStreamingAssets(relativePath);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return null;
    }

    /// <summary> Addressables 핸들을 취소 토큰과 함께 대기. </summary>
    private static async UniTask<T> AwaitWithCancellation<T>(AsyncOperationHandle<T> handle, CancellationToken token)
    {
        await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: token);

        if (handle.Status == AsyncOperationStatus.Failed)
        {
            Exception ex = handle.OperationException ?? new Exception("Addressables operation failed.");
            throw ex;
        }

        return handle.Result;
    }

    /// <summary> StreamingAssets 폴더에서 텍스처 동기 로드. </summary>
    private static Texture2D LoadTextureFromStreamingAssets(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;

        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

        if (!File.Exists(fullPath)) return null;

        byte[] fileData = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool ok = texture.LoadImage(fileData);

        if (ok)
        {
            texture.wrapMode = TextureWrapMode.Clamp; 
            return texture;
        }
        
        return null;
    }

    /// <summary> 컴포넌트가 없으면 추가해서 반환. </summary>
    public static T GetOrAdd<T>(GameObject go) where T : Component
    {
        if (!go) return null;

        if (go.TryGetComponent(out T component)) return component;
        component = go.AddComponent<T>();
        return component;
    }

    /// <summary> RectTransform 속성(크기, 위치, 회전, 스케일) 일괄 적용. </summary>
    private static void ApplyRect(RectTransform rt, Vector2? size = null, Vector2? anchoredPos = null, Vector3? rotation = null, Vector3? scale = null)
    {
        if (!rt) return;

        if (size.HasValue) rt.sizeDelta = size.Value;
        if (anchoredPos.HasValue) rt.anchoredPosition = anchoredPos.Value;
        if (rotation.HasValue) rt.localRotation = Quaternion.Euler(rotation.Value);
        if (scale.HasValue) rt.localScale = scale.Value;
    }

    #endregion
}