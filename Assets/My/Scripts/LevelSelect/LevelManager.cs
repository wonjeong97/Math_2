using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary> 레벨 선택 씬(LevelSelect)의 설정 데이터 클래스. </summary>
[Serializable]
public class LevelSetting
{   
    // --- 공통 배경 설정 ---
    public ImageSetting commonBackground;       // 전체 공통 배경
    
    // --- 1페이지 (레벨 선택) 설정 ---
    public TextSetting infoText;                // 안내 텍스트
    public ButtonSetting[] levelButtons;        // 레벨 버튼 설정 배열

    // --- 2페이지 (게임 타입 선택) 설정 ---
    public ImageSetting[] gameLevelImages;      // 상단 타이틀 이미지 ("Level N Game")
    
    public TextSetting page2InfoText;           // 2페이지 안내 텍스트
    public GradientData[] levelGradients;       // 레벨별 텍스트 그라데이션

    // 게임 타입 버튼 설정
    public ButtonSetting buttonTypeGuessNumber;
    public ButtonSetting buttonTypeCalculateNumber;
    public ButtonSetting buttonTypeNumberSystem;
    
    public ImageSetting[] guessNumberLevelImages;     
    public ImageSetting[] calculateNumberLevelImages; 
    public ImageSetting[] numberSystemLevelImages;    

    public ButtonSetting buttonBack;            // 뒤로가기 버튼
}

/// <summary> 레벨 및 게임 타입 선택 화면을 관리하는 매니저. </summary>
public sealed class LevelManager : BaseManager<LevelSetting>
{
    [Header("Pages")]
    [SerializeField] private GameObject pageLevel;   // 1페이지
    [SerializeField] private GameObject pageType;    // 2페이지

    [Header("Backgrounds")]
    [SerializeField] private GameObject backgroundObj; // 공통 배경 오브젝트

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

    protected override string JsonPath => "JSON/LevelSetting.json";

    private int _selectedLevel = -1;

    /// <summary> 초기화 진입점 </summary>
    protected override async UniTask Initialize()
    {
        try
        {
            // 공통 배경 설정
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

            // 씬 시작 페이드 인
            if (fader != null && fadeImage != null)
            {
                await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
            }
            
            SoundManager.Instance?.PlayBGM("LevelSelect_BGM");
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

    private async UniTask SetupLevelButtonsUIAsync()
    {
        if (ui == null || managerSetting == null || managerSetting.levelButtons == null) return;
        int count = Mathf.Min(levelButtons.Length, managerSetting.levelButtons.Length);
        for (int i = 0; i < count; i++)
        {
            if (levelButtons[i] == null) continue;
            await ui.SetButtonObj(levelButtons[i].gameObject, managerSetting.levelButtons[i], DestroyToken);
        }
    }

    private async UniTask SetupTypeButtonsUIAsync()
    {
        if (ui == null || managerSetting == null) return;
        if (buttonTypeGuessNumber != null) await ui.SetButtonObj(buttonTypeGuessNumber.gameObject, managerSetting.buttonTypeGuessNumber, DestroyToken);
        if (buttonTypeCalculateNumber != null) await ui.SetButtonObj(buttonTypeCalculateNumber.gameObject, managerSetting.buttonTypeCalculateNumber, DestroyToken);
        if (buttonTypeNumberSystem != null) await ui.SetButtonObj(buttonTypeNumberSystem.gameObject, managerSetting.buttonTypeNumberSystem, DestroyToken);
        if (buttonBack != null) await ui.SetButtonObj(buttonBack.gameObject, managerSetting.buttonBack, DestroyToken);
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
            // [중요] 비동기 핸들러 연결
            btn.onClick.AddListener(() => OnClickLevel(levelIndex).Forget());
        }
    }

    private void SetupTypeButtonListeners()
    {
        if (buttonTypeGuessNumber != null) { buttonTypeGuessNumber.onClick.RemoveAllListeners(); buttonTypeGuessNumber.onClick.AddListener(() => OnClickGameType(GameType.GuessNumber)); }
        if (buttonTypeCalculateNumber != null) { buttonTypeCalculateNumber.onClick.RemoveAllListeners(); buttonTypeCalculateNumber.onClick.AddListener(() => OnClickGameType(GameType.CalculateNumber)); }
        if (buttonTypeNumberSystem != null) { buttonTypeNumberSystem.onClick.RemoveAllListeners(); buttonTypeNumberSystem.onClick.AddListener(() => OnClickGameType(GameType.NumberSystem)); }
        
        // [중요] 비동기 핸들러 연결
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

    /// <summary> 레벨 버튼 클릭 핸들러 (1페이지 -> 2페이지) </summary>
    private async UniTaskVoid OnClickLevel(int level)
    {   
        SoundManager.Instance?.PlaySFX("Button");
        _selectedLevel = level;
        LevelSelectContext.SelectedLevel = level;

        Debug.Log($"[LevelManager] Player Clicked Level: {level}");

        // 1. Fade Out (화면 깜빡임 방지)
        if (fader != null && fadeImage != null)
        {
            await fader.FadeOut(fadeImage, fadeTime * 0.5f, DestroyToken); 
        }

        // 2. 페이지 전환 (오브젝트 활성화)
        ShowPageType();

        // 3. UI 갱신 및 비디오 로드 (오브젝트가 켜진 상태여야 VideoPlayer 오류가 안 남)
        ApplySelectedLevelImage(level);
        ApplyGameTypeImages(level);
        ApplyPage2TextGradient(level);

        // 4. Fade In (화면 밝아짐)
        if (fader != null && fadeImage != null)
        {
            await fader.FadeIn(fadeImage, fadeTime * 0.5f, DestroyToken);
        }
    }

    /// <summary> 뒤로가기 버튼 핸들러 (2페이지 -> 1페이지) </summary>
    private async UniTaskVoid OnClickBack()
    {   
        SoundManager.Instance?.PlaySFX("Button");
        Debug.Log($"[LevelManager] Player Clicked Back");

        // 1. Fade Out
        if (fader != null && fadeImage != null)
        {
            await fader.FadeOut(fadeImage, fadeTime * 0.5f, DestroyToken);
        }

        // 2. 페이지 전환
        ShowPageLevel();

        // 3. Fade In
        if (fader != null && fadeImage != null)
        {
            await fader.FadeIn(fadeImage, fadeTime * 0.5f, DestroyToken);
        }
    }
    

    private void ApplySelectedLevelImage(int level)
    {
        if (selectedLevelImage == null || managerSetting?.gameLevelImages == null) return;
        ApplyImageFromSetting(selectedLevelImage, managerSetting.gameLevelImages, level);
    }

    private void ApplyGameTypeImages(int level)
    {
        if (buttonTypeGuessNumber != null && managerSetting.guessNumberLevelImages != null)
            ApplyImageFromSetting(buttonTypeGuessNumber.gameObject, managerSetting.guessNumberLevelImages, level);

        if (buttonTypeCalculateNumber != null && managerSetting.calculateNumberLevelImages != null)
            ApplyImageFromSetting(buttonTypeCalculateNumber.gameObject, managerSetting.calculateNumberLevelImages, level);

        if (buttonTypeNumberSystem != null && managerSetting.numberSystemLevelImages != null)
            ApplyImageFromSetting(buttonTypeNumberSystem.gameObject, managerSetting.numberSystemLevelImages, level);
    }

    private void ApplyImageFromSetting(GameObject targetObj, ImageSetting[] settings, int level)
    {
        int index = level - 1;
        if (index < 0 || index >= settings.Length) return;
        
        ImageSetting imgSetting = settings[index];
        if (imgSetting != null)
        {
            // 단순 이미지/비디오 교체는 await 없이 실행 (Forget)
            ui.SetImageObj(targetObj, imgSetting).Forget();
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
        
        SoundManager.Instance?.PlaySFX("Button");
        LevelSelectContext.SelectedGameType = type;
        Debug.Log($"[LevelManager] Player Clicked Type: {type}");
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
                case GameType.GuessNumber: targetSceneName = "GuessNumber"; break;
                case GameType.CalculateNumber: targetSceneName = "CalculateNumber"; break;
                case GameType.NumberSystem: targetSceneName = "NumberSystem"; break;
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