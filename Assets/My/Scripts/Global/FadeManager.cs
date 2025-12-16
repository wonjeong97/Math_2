using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 페이드 인/아웃 효과를 처리하는 매니저 클래스.
/// UniTask를 사용하여 비동기적으로 이미지의 투명도를 조절.
/// </summary>
public class FadeManager : MonoBehaviour
{   
    public static FadeManager Instance; // 싱글톤 인스턴스

    /// <summary> 싱글톤 초기화 및 씬 전환 시 파괴 방지 설정.</summary>
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

    /// <summary> 이미지의 알파(투명도) 값만 변경하는 헬퍼 메서드. </summary>
    private static void SetAlpha(Image image, float a)
    {
        Color c = image.color;
        c.a = a;
        image.color = c;
    }
    
    /// <summary>
    /// 페이드 아웃 (화면이 점점 어두워짐, Alpha 0 -> 1).
    /// 시작 시 RaycastTarget을 켜서 터치 입력을 차단.
    /// </summary>
    public async UniTask FadeOut(Image image, float duration, CancellationToken token = default)
    {
        if (image == null)
        {
            Debug.LogError("[FadeManager] FadeOut-> image is null");
            return;
        }
        if (!image.gameObject.activeInHierarchy) image.gameObject.SetActive(true);
        
        // 즉시 적용 (시간이 0 이하일 때)
        if (duration <= 0f)
        {
            SetAlpha(image, 1f);
            image.raycastTarget = true;
            return;
        }

        try
        {
            image.raycastTarget = true; // 입력 차단
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
            // 취소 시 현재 알파값 유지, RaycastTarget 상태 유지
        }
        catch (Exception e)
        {
            Debug.LogError($"[FadeManager] FadeOut-> Exception: {e}");
        }
    }
    
    /// <summary>
    /// 페이드 인 (화면이 점점 밝아짐, Alpha 1 -> 0).
    /// 완료 후 RaycastTarget을 꺼서 터치 입력을 허용.
    /// </summary>
    public async UniTask FadeIn(Image image, float duration, CancellationToken token = default)
    {
        if (image == null)
        {
            Debug.LogError("[FadeManager] FadeIn-> image is null");
            return;
        }

        if (!image.gameObject.activeInHierarchy) image.gameObject.SetActive(true);
        
        // 즉시 적용
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
            image.raycastTarget = false; // 입력 허용
        }
        catch (OperationCanceledException)
        {
            // 취소 시 현재 알파값 유지
        }
        catch (Exception e)
        {
            Debug.LogError($"[FadeManager] FadeIn-> Exception: {e}");
        }
    }
}
