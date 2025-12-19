using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 모든 매니저의 기본 추상 클래스.
/// 공통 싱글톤 참조 및 초기화 흐름 관리.
/// </summary>
public abstract class BaseManager<TSetting> : MonoBehaviour
{   
    [Header("Fade Image")]
    [SerializeField] protected Image fadeImage; // 페이드 이미지

    private Settings setting; // 전역 설정 (JsonLoader에서 로드됨)
    protected TSetting managerSetting; // 매니저별 설정 (자식 클래스용)
    protected abstract string JsonPath { get; } // JSON 파일 경로

    protected UIManager ui; // UI 매니저
    protected FadeManager fader; // 페이드 매니저
    protected CancellationToken DestroyToken => this.GetCancellationTokenOnDestroy(); // 취소 토큰

    protected float fadeTime; // 페이드 시간
    
    /// <summary>
    /// 초기화 진입점.
    /// 데이터 로드 및 초기화 실행.
    /// </summary>
    protected virtual async void Start()
    {
        try
        {
            // 1. 싱글톤 및 필수 매니저 확인
            if (JsonLoader.Instance == null) return;
            
            if (JsonLoader.Instance.settings != null) setting = JsonLoader.Instance.settings; 
            else return;

            if (UIManager.Instance != null) ui = UIManager.Instance; 
            else return;

            if (FadeManager.Instance != null) fader = FadeManager.Instance;
            else return;

            // 2. 데이터 로드
            managerSetting = JsonLoader.Instance.LoadJsonData<TSetting>(JsonPath);
            fadeTime = setting.fadeTime;

            // 4. 자식 클래스 초기화 호출
            await Initialize();
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning($"[{SceneManager.GetActiveScene().name}] => Start Canceled");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SceneManager.GetActiveScene().name}] => Start Exception: {e}");
        }
        finally
        {
            Debug.Log($"[{SceneManager.GetActiveScene().name}] => Start Complete");
        }
    }
    
    /// <summary> 매니저별 초기화 로직. </summary>
    protected abstract UniTask Initialize();
}