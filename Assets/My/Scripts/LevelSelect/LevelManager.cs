using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary> 레벨 선택 씬의 설정 데이터를 정의함. </summary>
[Serializable]
public class LevelSetting
{   
    public ImageSetting commonBackground;       
    
    public TextSetting infoText;                
    public ButtonSetting[] levelButtons;        

    public ImageSetting[] gameLevelImages;      
    public TextSetting page2InfoText;           
    public GradientData[] levelGradients;       

    public ButtonSetting buttonTypeGuessNumber;
    public ButtonSetting buttonTypeCalculateNumber;
    public ButtonSetting buttonTypeNumberSystem;
    
    public ImageSetting[] guessNumberLevelImages;     
    public ImageSetting[] calculateNumberLevelImages; 
    public ImageSetting[] numberSystemLevelImages;    

    public ButtonSetting buttonBack;            
}

/// <summary> 레벨 선택 화면을 관리함. </summary>
public sealed class LevelManager : BaseManager<LevelSetting>
{
    [Header("Pages")]
    [SerializeField] private GameObject pageLevel;   
    [SerializeField] private GameObject pageType;    

    [Header("Backgrounds")]
    [SerializeField] private GameObject backgroundObj; 

    [Header("Level Buttons")]
    [SerializeField] private Button[] levelButtons; 

    [Header("Type Buttons")]
    [SerializeField] private Button buttonTypeGuessNumber;      
    [SerializeField] private Button buttonTypeCalculateNumber;  
    [SerializeField] private Button buttonTypeNumberSystem;     

    [Header("Page2 UI")]
    [SerializeField] private GameObject selectedLevelImage;     
    [SerializeField] private Button buttonBack;                 

    [Header("Text")] 
    [SerializeField] private GameObject page1InfoText;          
    [SerializeField] private GameObject page2InfoTextObj;       

    protected override string JsonPath => GameConstants.Path.JsonLevelSetting;

    private int _selectedLevel = -1;

    protected override async UniTask Initialize()
    {
        try
        {
            if (backgroundObj != null && managerSetting.commonBackground != null)
            {
                await ui.SetImageObj(backgroundObj, managerSetting.commonBackground, DestroyToken);
            }
            
            ui.SetTextObj(page1InfoText, managerSetting.infoText).Forget();
            ui.SetTextObj(page2InfoTextObj, managerSetting.page2InfoText).Forget();
            
            await SetupLevelButtonsUIAsync();
            await SetupTypeButtonsUIAsync();

            SetupLevelButtonListeners();
            SetupTypeButtonListeners();

            ShowPageLevel();

            if (fader != null && fadeImage != null)
            {
                await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
            }
            
            SoundManager.Instance?.PlayBGM(GameConstants.Sound.LevelSelectBGM);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[LevelManager] Initialize-> Canceled");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] Initialize-> Exception: {e}");
        }
    }

    /// <summary> 1페이지 버튼을 설정함. </summary>
    private async UniTask SetupLevelButtonsUIAsync()
    {
        if (ui == null || managerSetting == null || managerSetting.levelButtons == null) return;
        if (levelButtons == null) return;

        int count = Mathf.Min(levelButtons.Length, managerSetting.levelButtons.Length);
        List<UniTask> tasks = new List<UniTask>();

        for (int i = 0; i < count; i++)
        {
            if (levelButtons[i] == null) continue;
            tasks.Add(ui.SetButtonObj(levelButtons[i].gameObject, managerSetting.levelButtons[i], DestroyToken));
        }

        await UniTask.WhenAll(tasks);
    }

    /// <summary> 2페이지 버튼을 설정함. </summary>
    private async UniTask SetupTypeButtonsUIAsync()
    {
        if (ui == null || managerSetting == null) return;
        
        List<UniTask> tasks = new List<UniTask>();

        if (buttonTypeGuessNumber != null) 
            tasks.Add(ui.SetButtonObj(buttonTypeGuessNumber.gameObject, managerSetting.buttonTypeGuessNumber, DestroyToken));
        
        if (buttonTypeCalculateNumber != null) 
            tasks.Add(ui.SetButtonObj(buttonTypeCalculateNumber.gameObject, managerSetting.buttonTypeCalculateNumber, DestroyToken));
        
        if (buttonTypeNumberSystem != null) 
            tasks.Add(ui.SetButtonObj(buttonTypeNumberSystem.gameObject, managerSetting.buttonTypeNumberSystem, DestroyToken));
        
        if (buttonBack != null) 
            tasks.Add(ui.SetButtonObj(buttonBack.gameObject, managerSetting.buttonBack, DestroyToken));

        await UniTask.WhenAll(tasks);
    }

    private void SetupLevelButtonListeners()
    {
        if (levelButtons == null) return;
        for (int i = 0; i < levelButtons.Length; i++)
        {
            Button btn = levelButtons[i];
            if (btn == null) continue;
            btn.onClick.RemoveAllListeners();
            int levelIndex = i + 1;
            btn.onClick.AddListener(() => OnClickLevel(levelIndex).Forget());
        }
    }

    private void SetupTypeButtonListeners()
    {
        if (buttonTypeGuessNumber != null) { buttonTypeGuessNumber.onClick.RemoveAllListeners(); buttonTypeGuessNumber.onClick.AddListener(() => OnClickGameType(GameType.GuessNumber)); }
        if (buttonTypeCalculateNumber != null) { buttonTypeCalculateNumber.onClick.RemoveAllListeners(); buttonTypeCalculateNumber.onClick.AddListener(() => OnClickGameType(GameType.CalculateNumber)); }
        if (buttonTypeNumberSystem != null) { buttonTypeNumberSystem.onClick.RemoveAllListeners(); buttonTypeNumberSystem.onClick.AddListener(() => OnClickGameType(GameType.NumberSystem)); }
        
        if (buttonBack != null) { buttonBack.onClick.RemoveAllListeners(); buttonBack.onClick.AddListener(() => OnClickBack().Forget()); }
    }

    private void ShowPageLevel()
    {
        if (pageLevel != null) pageLevel.SetActive(true);
        if (pageType != null) pageType.SetActive(false);
    }

    private void ShowPageType()
    {
        if (pageLevel != null) pageLevel.SetActive(false);
        if (pageType != null) pageType.SetActive(true);
    }

    /// <summary> 레벨 버튼 클릭을 처리함 (1페이지 -> 2페이지). </summary>
    private async UniTaskVoid OnClickLevel(int level)
    {
        try
        {   
            if (GameManager.Instance) GameManager.Instance.ResetInactivityTimer();
            SoundManager.Instance?.PlaySFX(GameConstants.Sound.ButtonClick);
            _selectedLevel = level;
            LevelSelectContext.SelectedLevel = level;

            Debug.Log($"[LevelManager] Player Clicked Level: {level}");

            if (fader != null && fadeImage != null)
            {
                await fader.FadeOut(fadeImage, fadeTime * 0.5f, DestroyToken); 
            }

            ShowPageType();

            var t1 = ApplySelectedLevelImageAsync(level);
            var t2 = ApplyGameTypeImagesAsync(level);
        
            ApplyPage2TextGradient(level);

            await UniTask.WhenAll(t1, t2);

            if (fader != null && fadeImage != null)
            {
                await fader.FadeIn(fadeImage, fadeTime * 0.5f, DestroyToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] OnClickLevel-> Exception: {e}");
        }
    }

    /// <summary> 뒤로가기 버튼 클릭을 처리함 (2페이지 -> 1페이지). </summary>
    private async UniTaskVoid OnClickBack()
    {
        try
        {   
            if (GameManager.Instance) GameManager.Instance.ResetInactivityTimer();
            SoundManager.Instance?.PlaySFX(GameConstants.Sound.ButtonClick);

            if (fader != null && fadeImage != null)
            {
                await fader.FadeOut(fadeImage, fadeTime * 0.5f, DestroyToken);
            }

            ShowPageLevel();

            await SetupLevelButtonsUIAsync();

            if (fader != null && fadeImage != null)
            {
                await fader.FadeIn(fadeImage, fadeTime * 0.5f, DestroyToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] OnClickBack-> Exception: {e}");
        }
    }

    private async UniTask ApplySelectedLevelImageAsync(int level)
    {
        if (selectedLevelImage == null || managerSetting?.gameLevelImages == null) return;
        await ApplyImageFromSettingAsync(selectedLevelImage, managerSetting.gameLevelImages, level);
    }

    private async UniTask ApplyGameTypeImagesAsync(int level)
    {
        List<UniTask> tasks = new List<UniTask>();

        if (buttonTypeGuessNumber != null && managerSetting.guessNumberLevelImages != null)
            tasks.Add(ApplyImageFromSettingAsync(buttonTypeGuessNumber.gameObject, managerSetting.guessNumberLevelImages, level));

        if (buttonTypeCalculateNumber != null && managerSetting.calculateNumberLevelImages != null)
            tasks.Add(ApplyImageFromSettingAsync(buttonTypeCalculateNumber.gameObject, managerSetting.calculateNumberLevelImages, level));

        if (buttonTypeNumberSystem != null && managerSetting.numberSystemLevelImages != null)
            tasks.Add(ApplyImageFromSettingAsync(buttonTypeNumberSystem.gameObject, managerSetting.numberSystemLevelImages, level));

        await UniTask.WhenAll(tasks);
    }

    private async UniTask ApplyImageFromSettingAsync(GameObject targetObj, ImageSetting[] settings, int level)
    {
        int index = level - 1;
        if (index < 0 || index >= settings.Length) return;
        
        ImageSetting imgSetting = settings[index];
        if (imgSetting != null)
        {
            await ui.SetImageObj(targetObj, imgSetting, DestroyToken);
        }
    }
    
    private void ApplyPage2TextGradient(int level)
    {
        if (page2InfoTextObj == null || managerSetting.levelGradients == null) return;
        int index = level - 1;
        if (index < 0 || index >= managerSetting.levelGradients.Length) return;

        GradientData data = managerSetting.levelGradients[index];
        var gradientComp = UIManager.GetOrAdd<TextGlobalGradient>(page2InfoTextObj);
        if (gradientComp != null)
        {
            gradientComp.enabled = true; 
            gradientComp.SetGradient(data.topLeft, data.topRight, data.bottomLeft, data.bottomRight);
        }
    }

    private void OnClickGameType(GameType type)
    {
        if (_selectedLevel <= 0)
        {
            Debug.LogError("[LevelManager] OnClickGameType-> Level is not selected.");
            return;
        }
        if (GameManager.Instance) GameManager.Instance.ResetInactivityTimer();
        SoundManager.Instance?.PlaySFX(GameConstants.Sound.ButtonClick);
        LevelSelectContext.SelectedGameType = type;
        HandleLoadGameSceneAsync().Forget();
    }

    private async UniTask HandleLoadGameSceneAsync()
    {
        try
        {
            if (fader != null && fadeImage != null)
            {
                await fader.FadeOut(fadeImage, fadeTime, DestroyToken);
            }

            string targetSceneName = "";
            switch (LevelSelectContext.SelectedGameType)
            {
                case GameType.GuessNumber: targetSceneName = GameConstants.Scene.GuessNumber; break;
                case GameType.CalculateNumber: targetSceneName = GameConstants.Scene.CalculateNumber; break;
                case GameType.NumberSystem: targetSceneName = GameConstants.Scene.NumberSystem; break;
                default: return;
            }

            AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            if (op == null) return;

            while (!op.isDone)
            {
                DestroyToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, DestroyToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] HandleLoadGameSceneAsync-> Exception: {e}");
        }
    }
}