using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public static class GameResultContext
{
    /// <summary> 맞춘 문제 개수 (0 ~ 4) </summary>
    public static int CorrectCount { get; set; } = 0;
}

[Serializable]
public class GameEndSetting
{
    public ImageSetting gameEndImage;
    public ImageSetting[] scoreResultImages;
    public ButtonSetting homeButton;
}

/// <summary>
/// 게임 종료(결과) 화면을 관리하는 매니저.
/// 점수별 이미지 표시 및 타이틀 복귀 기능을 담당.
/// </summary>
public class GameEndManager : BaseManager<GameEndSetting>
{
    [Header("UI Objects")]
    [SerializeField] private Image uiGameEndImage;  // 배경/타이틀 이미지
    [SerializeField] private Image uiMyScoreImage;  // 점수(별) 결과 이미지
    [SerializeField] private Button uiHomeButton;   // 홈(타이틀) 복귀 버튼
    
    // JSON 파일명 정의
    protected override string JsonPath => "JSON/GameEnd.json";
    
    /// <summary>
    /// 초기화 진입점.
    /// UI 설정, 버튼 이벤트 연결, 화면 페이드 인 실행.
    /// </summary>
    protected override async UniTask Initialize()
    {   
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("GameEnd_BGM");
        }
        
        // 1. UI 설정 적용
        ApplyUISettings();
        
        // 2. 버튼 이벤트 설정
        SetupButtonEvents();

        // 3. 씬 시작 시 Fade In (검정 -> 투명)
        if (fader != null && fadeImage != null)
        {
            await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
        }
    }
    
    /// <summary> JSON 설정 데이터를 기반으로 UI(이미지, 버튼) 초기화. </summary>
    private void ApplyUISettings()
    {
        // BaseManager의 ui (UIManager)와 managerSetting 사용
        if (ui == null || managerSetting == null) return;

        // 1. 기본 배경/타이틀 이미지 설정
        if (uiGameEndImage != null && managerSetting.gameEndImage != null)
        {
            ui.SetImageObj(uiGameEndImage.gameObject, managerSetting.gameEndImage);
        }

        // 2. 홈 버튼 설정
        if (uiHomeButton != null && managerSetting.homeButton != null)
        {
            ui.SetButtonObj(uiHomeButton.gameObject, managerSetting.homeButton).Forget();
        }

        // 3. 점수(Score) 이미지 설정 (맞춘 개수에 따라 다르게 표시)
        if (uiMyScoreImage != null && managerSetting.scoreResultImages != null)
        {
            int score = Mathf.Clamp(GameResultContext.CorrectCount, 0, 4);

            if (score < managerSetting.scoreResultImages.Length)
            {
                ImageSetting scoreImgData = managerSetting.scoreResultImages[score];
                if (scoreImgData != null)
                {
                    ui.SetImageObj(uiMyScoreImage.gameObject, scoreImgData);
                }
            }
            else
            {
                Debug.LogWarning($"[GameEndManager] Score {score}에 해당하는 이미지 세팅이 없습니다.");
            }
        }
    }
    
    /// <summary> 버튼 클릭 이벤트 리스너 등록. </summary>
    private void SetupButtonEvents()
    {
        if (uiHomeButton != null)
        {
            uiHomeButton.onClick.RemoveAllListeners();
            uiHomeButton.onClick.AddListener(() =>
            {
                // 비동기 핸들러 호출
                HandleHomeButtonAsync().Forget();
            });
        }
    }

    /// <summary>
    /// 홈 버튼 클릭 처리.
    /// 페이드 아웃 후 타이틀 씬으로 이동.
    /// </summary>
    private async UniTaskVoid HandleHomeButtonAsync()
    {
        try
        {   
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX("Button");    
            }
            
            // 1. Fade Out
            if (fader != null && fadeImage != null)
            {
                await fader.FadeOut(fadeImage, fadeTime, DestroyToken);
            }
            
            Debug.Log("[GameEnd] Player Clicked Home");

            // 2. 타이틀 씬으로 이동
            SceneManager.LoadScene("Title");
        }
        catch (OperationCanceledException)
        {
            // 씬 전환 등으로 인한 작업 취소 (정상)
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameEndManager] HandleHomeButtonAsync Error: {e}");
        }
    }
}