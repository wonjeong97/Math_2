using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

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

    /// <summary>이미지 알파만 변경</summary>
    private static void SetAlpha(Image image, float a)
    {
        Color c = image.color;
        c.a = a;
        image.color = c;
    }
    
    /// <summary>이미지 알파를 0->1로 페이드 (시작 시 raycastTarget ON)</summary>
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
        catch (OperationCanceledException)
        {
            // 취소 시 현재 알파 유지, raycastTarget은 변경하지 않음
        }
        catch (Exception e)
        {
            Debug.LogError($"[FadeManager] FadeOut-> Exception: {e}");
        }
    }
    
    /// <summary>이미지 알파를 1->0로 페이드 (완료 후 raycastTarget OFF)</summary>
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
        catch (OperationCanceledException)
        {
            // 취소 시 현재 알파 유지, raycastTarget은 변경하지 않음
        }
        catch (Exception e)
        {
            Debug.LogError($"[FadeManager] FadeIn-> Exception: {e}");
        }
    }
}
