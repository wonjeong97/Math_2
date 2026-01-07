using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public static class GameResultContext
{
    /// <summary> 맞춘 문제 개수를 저장함 (0 ~ 4). </summary>
    public static int CorrectCount { get; set; } = 0;
}

[Serializable]
public class GameEndSetting
{
    public ImageSetting backgroundImage;
    public ImageSetting gameEndImage;
    public ImageSetting[] scoreResultImages;
    public ButtonSetting homeButton;
}

/// <summary> 게임 종료 화면을 관리함 (점수 표시 및 홈 복귀). </summary>
public class GameEndManager : BaseManager<GameEndSetting>
{
    [Header("UI Objects")]
    [SerializeField] private GameObject backgroundObj;
    [SerializeField] private Image uiGameEndImage;  
    [SerializeField] private Image uiMyScoreImage;  
    [SerializeField] private Button uiHomeButton;   
    
    protected override string JsonPath => GameConstants.Path.JsonGameEnd;
    
    protected override async UniTask Initialize()
    {   
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(GameConstants.Sound.GameEndBGM);
        }
        
        if (backgroundObj != null && managerSetting != null && managerSetting.backgroundImage != null && UIManager.Instance != null)
        {
            UIManager.Instance.SetImageObj(backgroundObj, managerSetting.backgroundImage, this.GetCancellationTokenOnDestroy()).Forget();
        }
        
        ApplyUISettings();
        SetupButtonEvents();

        if (fader != null && fadeImage != null)
        {
            await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
        }
    }
    
    /// <summary> JSON 설정을 기반으로 UI를 초기화함. </summary>
    private void ApplyUISettings()
    {
        if (ui == null || managerSetting == null) return;

        if (uiGameEndImage != null && managerSetting.gameEndImage != null)
        {
            ui.SetImageObj(uiGameEndImage.gameObject, managerSetting.gameEndImage, this.GetCancellationTokenOnDestroy()).Forget();
        }

        if (uiHomeButton != null && managerSetting.homeButton != null)
        {
            ui.SetButtonObj(uiHomeButton.gameObject, managerSetting.homeButton, this.GetCancellationTokenOnDestroy()).Forget();
        }

        if (uiMyScoreImage != null && managerSetting.scoreResultImages != null)
        {
            int score = Mathf.Clamp(GameResultContext.CorrectCount, 0, 4);

            if (score < managerSetting.scoreResultImages.Length)
            {
                ImageSetting scoreImgData = managerSetting.scoreResultImages[score];
                if (scoreImgData != null)
                {
                    ui.SetImageObj(uiMyScoreImage.gameObject, scoreImgData, this.GetCancellationTokenOnDestroy()).Forget();
                }
            }
            else
            {
                Debug.LogWarning($"[GameEndManager] Score {score}에 해당하는 이미지 세팅이 없음.");
            }
        }
    }
    
    /// <summary> 버튼 이벤트를 등록함. </summary>
    private void SetupButtonEvents()
    {
        if (uiHomeButton != null)
        {
            uiHomeButton.onClick.RemoveAllListeners();
            uiHomeButton.onClick.AddListener(() =>
            {
                HandleHomeButtonAsync().Forget();
            });
        }
    }

    /// <summary> 홈 버튼 클릭을 처리함. </summary>
    private async UniTaskVoid HandleHomeButtonAsync()
    {
        try
        {   
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(GameConstants.Sound.ButtonClick);    
            }
            
            if (fader != null && fadeImage != null)
            {
                await fader.FadeOut(fadeImage, fadeTime, DestroyToken);
            }
            
            SceneManager.LoadScene(GameConstants.Scene.Title);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[GameEndManager] HandleHomeButtonAsync Error: {e}");
        }
    }
}