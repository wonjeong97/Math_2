using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class TitleSetting
{
    public ImageSetting titleBackgroundImage;
    public ButtonSetting titleStartButton;
    public TextSetting titleText1;
    public TextSetting titleText2;
}

/// <summary> 타이틀 화면을 관리함. </summary>
public sealed class TitleManager : BaseManager<TitleSetting>
{   
    [Header("UI")]
    [SerializeField] private GameObject titleBackgroundImage; 
    [SerializeField] private GameObject titleStartButton;     
    [SerializeField] private GameObject titleText1;           
    [SerializeField] private GameObject titleText2;           
    
    protected override string JsonPath => GameConstants.Path.JsonTitleSetting;
   
    /// <summary> 초기화를 진행함 (UI 설정 및 페이드 인). </summary>
    protected override async UniTask Initialize()
    {
        try
        {   
            SoundManager.Instance?.PlayBGM(null);
            
            if (titleBackgroundImage != null && managerSetting.titleBackgroundImage != null)
            {
                await ui.SetImageObj(titleBackgroundImage, managerSetting.titleBackgroundImage, DestroyToken);
            }
            ui.SetTextObj(titleText1, managerSetting.titleText1).Forget();
            ui.SetTextObj(titleText2, managerSetting.titleText2).Forget();
            await ui.SetButtonObj(titleStartButton, managerSetting.titleStartButton, DestroyToken);
            
            Button startBtn = titleStartButton != null ? titleStartButton.GetComponent<Button>() : null;
            if (startBtn != null)
            {
                startBtn.onClick.RemoveListener(OnStartButtonClicked);
                startBtn.onClick.AddListener(OnStartButtonClicked);
            }
            
            await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[Title] => Initialize Canceled");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Title] => Initialize Exception: {e}");
        }
        finally
        {
            Debug.Log("[Title] => Initialize Finished");
        }
    }
    
    /// <summary> 시작 버튼 클릭을 처리함. </summary>
    private void OnStartButtonClicked()
    {   
        SoundManager.Instance?.PlaySFX(GameConstants.Sound.Start);
        HandleStartButtonAsync().Forget();
    }
    
    /// <summary> 시작 버튼 로직을 비동기로 처리함 (페이드 아웃 후 씬 이동). </summary>
    private async UniTask HandleStartButtonAsync()
    {
        try
        {
            await fader.FadeOut(fadeImage, fadeTime, DestroyToken);
            
            Debug.Log("[Title] Player Clicked Start");
            
            AsyncOperation op = SceneManager.LoadSceneAsync(GameConstants.Scene.LevelSelect, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[Title] HandleStartButtonAsync-> LoadSceneAsync returned null");
                return;
            }
            
            while (!op.isDone)
            {
                DestroyToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, DestroyToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[Title] HandleStartButtonAsync-> Exception: {e}");
        }
    }
}