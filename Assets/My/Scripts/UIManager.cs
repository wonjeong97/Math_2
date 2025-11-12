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

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

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

    /// <summary> TextObject 설정: 폰트/문구/색/정렬/RectTransform 반영 </summary>
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

            await ApplyFontAsync(tmp, textSetting.fontName, text, textSetting.fontSize, textSetting.fontColor, textSetting.alignment, token);
            ApplyRect(rt, size: null, anchoredPos: new Vector2(textSetting.position.x, -textSetting.position.y), rotation: textSetting.rotation);
        }
    }

    /// <summary> ImageObject 설정: 스트리밍 에셋 이미지 로드/타입/RectTransform 반영 </summary>
    public void SetImageObj(GameObject imageObj, ImageSetting imageSetting)
    {
        if (!imageObj || imageSetting == null)
        {
            Debug.LogError("[UIManager] SetImageObj => imageObj or imageSetting is null");
            return;
        }

        if (imageObj.TryGetComponent(out Image img) && imageObj.TryGetComponent(out RectTransform rt))
        {
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

        ApplyRect(buttonRt, size: buttonSetting.size, anchoredPos: new Vector2(buttonSetting.position.x, -buttonSetting.position.y), rotation: buttonSetting.rotation, scale: buttonSetting.scale);

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

        if (buttonSetting.buttonText != null)
        {
            TextMeshProUGUI tmp = buttonObj.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) await SetTextObj(tmp.gameObject, buttonSetting.buttonText, overrideText, token);
        }
    }

    #region UICreator

    private readonly List<GameObject> _instances = new List<GameObject>();
    private readonly Dictionary<string, AsyncOperationHandle> _assetCache = new Dictionary<string, AsyncOperationHandle>();

    /// <summary>Addressables 에셋 로드를 캐시해 중복 로드 방지</summary>
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

    /// <summary>폰트 키를 FontMap 기준으로 해석해 매핑된 키 반환</summary>
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

    private async UniTask ApplyFontAsync(TextMeshProUGUI uiText, string fontKey, string textValue, float fontSize, Color fontColor, TextAlignmentOptions alignment, CancellationToken token)
    {
        if (!uiText || string.IsNullOrEmpty(fontKey)) return;

        string mapped = ResolveFontKey(fontKey);
        TMP_FontAsset font = await LoadAssetWithCacheAsync<TMP_FontAsset>(mapped, token);
        if (font == null) return;

        token.ThrowIfCancellationRequested();

        uiText.font = font;
        uiText.fontSize = fontSize;
        uiText.color = fontColor;
        uiText.alignment = alignment;
        uiText.text = textValue;
    }

    #endregion

    #region UIUtility

    /// <summary> Addressables 핸들을 취소 토큰과 함께 대기 후 결과 반환 </summary>
    private static async UniTask<T> AwaitWithCancellation<T>(AsyncOperationHandle<T> handle, CancellationToken token)
    {
        // 취소 또는 완료까지 프레임 단위 대기
        await UniTask.WaitUntil(() => handle.IsDone, cancellationToken: token);

        if (handle.Status == AsyncOperationStatus.Failed)
        {
            Exception ex = handle.OperationException ?? new Exception("Addressables operation failed.");
            throw ex;
        }

        return handle.Result;
    }

    /// <summary> StreamingAssets 하위 경로에서 Texture2D 로드 (동기) </summary>
    private static Texture2D LoadTextureFromStreamingAssets(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;

        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

        if (!File.Exists(fullPath)) return null;

        byte[] fileData = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        bool ok = texture.LoadImage(fileData);

        return !ok ? null : texture;
    }

    /// <summary> 타입 T 컴포넌트를 가져오거나 없으면 추가해서 반환 </summary>
    public static T GetOrAdd<T>(GameObject go) where T : Component
    {
        if (!go) return null;

        if (go.TryGetComponent(out T component)) return component;
        component = go.AddComponent<T>();
        return component;
    }

    /// <summary> RectTransform 기본 속성 적용(size, anchoredPos, rotation, scale) </summary>
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