using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary> 화면 페이드 인/아웃 효과를 처리함. </summary>
public class FadeManager : MonoBehaviour
{   
    public static FadeManager Instance;

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

    private static void SetAlpha(Image image, float a)
    {
        Color c = image.color;
        c.a = a;
        image.color = c;
    }
    
    /// <summary>
    /// 페이드 아웃을 수행함 (화면이 어두워짐, Alpha 0 -> 1).
    /// 입력 차단을 위해 RaycastTarget을 켬.
    /// </summary>
    public async UniTask FadeOut(Image image, float duration, CancellationToken token = default)
    {
        if (image == null)
        {
            Debug.LogError("[FadeManager] FadeOut-> image is null");
            return;
        }
        if (!image.gameObject.activeInHierarchy) image.gameObject.SetActive(true);
        
        if (duration <= 0f)
        {
            SetAlpha(image, 1f);
            image.raycastTarget = true;
            return;
        }

        try
        {
            image.raycastTarget = true; 
            SetAlpha(image, 0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(image, t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetAlpha(image, 1f);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[FadeManager] FadeOut-> Exception: {e}");
        }
    }
    
    /// <summary>
    /// 페이드 인을 수행함 (화면이 밝아짐, Alpha 1 -> 0).
    /// 완료 후 입력 허용을 위해 RaycastTarget을 끔.
    /// </summary>
    public async UniTask FadeIn(Image image, float duration, CancellationToken token = default)
    {
        if (image == null)
        {
            Debug.LogError("[FadeManager] FadeIn-> image is null");
            return;
        }

        if (!image.gameObject.activeInHierarchy) image.gameObject.SetActive(true);
        
        if (duration <= 0f)
        {
            SetAlpha(image, 0f);
            image.raycastTarget = false;
            return;
        }

        try
        {
            SetAlpha(image, 1f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(image, 1f - t);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetAlpha(image, 0f);
            image.raycastTarget = false; 
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[FadeManager] FadeIn-> Exception: {e}");
        }
    }
}