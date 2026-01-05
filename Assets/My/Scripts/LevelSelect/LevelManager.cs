using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary> 레벨 선택 씬(LevelSelect)의 설정 데이터 클래스. </summary>
[Serializable]
public class LevelSetting
{
    // --- 1페이지 (레벨 선택) 설정 ---
    public ImageSetting pageLevelBackground;    // 1페이지 배경
    public TextSetting infoText;                // 안내 텍스트 (예: "레벨을 선택하세요")
    public ButtonSetting[] levelButtons;        // 레벨 버튼 설정 배열

    // --- 2페이지 (게임 타입 선택) 설정 ---
    public ImageSetting pageTypeBackground;     // 2페이지 배경
    public ImageSetting[] gameLevelImages;      // 상단 타이틀 이미지 ("Level N Game")
    
    public TextSetting page2InfoText;           // 2페이지 안내 텍스트
    
    // 레벨별 텍스트 그라데이션 (인덱스 0 = 레벨 1)
    public GradientData[] levelGradients;

    // 기본 버튼 설정 (위치/크기 초기화용)
    public ButtonSetting buttonTypeGuessNumber;
    public ButtonSetting buttonTypeCalculateNumber;
    public ButtonSetting buttonTypeNumberSystem;
    
    // 레벨별 게임 타입 버튼 이미지 (인덱스 0 = 레벨 1)
    public ImageSetting[] guessNumberLevelImages;     // 수 맞추기
    public ImageSetting[] calculateNumberLevelImages; // 계산하기
    public ImageSetting[] numberSystemLevelImages;    // 수의 체계

    public ButtonSetting buttonBack;            // 뒤로가기 버튼
}

/// <summary>
/// 레벨 및 게임 타입 선택 화면을 관리하는 매니저.
/// 1페이지(레벨) -> 2페이지(게임 타입) 순서로 진행.
/// </summary>
public sealed class LevelManager : BaseManager<LevelSetting>
{
    [Header("Pages")]
    [SerializeField] private GameObject pageLevel;   // 1페이지 (레벨 선택)
    [SerializeField] private GameObject pageType;    // 2페이지 (타입 선택)

    [Header("Backgrounds")]
    [SerializeField] private GameObject pageLevelBackgroundObj; // 1페이지 배경 오브젝트
    [SerializeField] private GameObject pageTypeBackgroundObj;  // 2페이지 배경 오브젝트

    [Header("Level Buttons (1~N)")]
    [SerializeField] private Button[] levelButtons;  // 레벨 버튼 배열 (인덱스 0 -> Level 1)

    [Header("Type Buttons")]
    [SerializeField] private Button buttonTypeGuessNumber;      // 수 맞추기 버튼
    [SerializeField] private Button buttonTypeCalculateNumber;  // 계산하기 버튼
    [SerializeField] private Button buttonTypeNumberSystem;     // 수의 체계 버튼

    [Header("Page2 UI")]
    [SerializeField] private GameObject selectedLevelImage;     // 상단 타이틀 이미지
    [SerializeField] private Button buttonBack;                 // 뒤로가기 버튼

    [Header("Text")] 
    [SerializeField] private GameObject page1InfoText;          // 1페이지 안내 텍스트
    [SerializeField] private GameObject page2InfoTextObj;       // 2페이지 안내 텍스트

    // JSON 파일명 정의
    protected override string JsonPath => "JSON/LevelSetting.json";

    private int _selectedLevel = -1; // 현재 선택된 레벨

    /// <summary>
    /// 초기화 진입점.
    /// UI 설정, 리스너 등록, 1페이지 표시 및 페이드 인.
    /// </summary>
    protected override async UniTask Initialize()
    {
        try
        {
            await ApplyBackgroundsAsync();
            
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

    /// <summary> 배경 이미지 설정. </summary>
    private async UniTask ApplyBackgroundsAsync()
    {
        if (ui == null || managerSetting == null) return;

        // 1페이지 배경
        if (pageLevelBackgroundObj != null && managerSetting.pageLevelBackground != null)
        {
            await ui.SetImageObj(pageLevelBackgroundObj, managerSetting.pageLevelBackground, DestroyToken);
        }

        // 2페이지 배경
        if (pageTypeBackgroundObj != null && managerSetting.pageTypeBackground != null)
        {
            await ui.SetImageObj(pageTypeBackgroundObj, managerSetting.pageTypeBackground, DestroyToken);
        }
    }

    /// <summary> 레벨 버튼 UI 비동기 설정. </summary>
    private async UniTask SetupLevelButtonsUIAsync()
    {
        if (ui == null || managerSetting == null || managerSetting.levelButtons == null) return;
        if (levelButtons == null) return;

        int count = Mathf.Min(levelButtons.Length, managerSetting.levelButtons.Length);

        for (int i = 0; i < count; i++)
        {
            Button button = levelButtons[i];
            ButtonSetting setting = managerSetting.levelButtons[i];

            if (button == null || setting == null) continue;

            try
            {
                await ui.SetButtonObj(button.gameObject, setting, DestroyToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogError($"[LevelManager] SetupLevelButtonsUIAsync-> Exception on index {i}: {e}");
            }
        }
    }

    /// <summary> 게임 타입 버튼 UI 비동기 설정. </summary>
    private async UniTask SetupTypeButtonsUIAsync()
    {
        if (ui == null || managerSetting == null) return;

        try
        {
            if (buttonTypeGuessNumber != null && managerSetting.buttonTypeGuessNumber != null)
                await ui.SetButtonObj(buttonTypeGuessNumber.gameObject, managerSetting.buttonTypeGuessNumber, DestroyToken);

            if (buttonTypeCalculateNumber != null && managerSetting.buttonTypeCalculateNumber != null)
                await ui.SetButtonObj(buttonTypeCalculateNumber.gameObject, managerSetting.buttonTypeCalculateNumber, DestroyToken);

            if (buttonTypeNumberSystem != null && managerSetting.buttonTypeNumberSystem != null)
                await ui.SetButtonObj(buttonTypeNumberSystem.gameObject, managerSetting.buttonTypeNumberSystem, DestroyToken);

            if (buttonBack != null && managerSetting.buttonBack != null)
                await ui.SetButtonObj(buttonBack.gameObject, managerSetting.buttonBack, DestroyToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] SetupTypeButtonsUIAsync-> Exception: {e}");
        }
    }

    /// <summary> 레벨 버튼 클릭 리스너 등록. </summary>
    private void SetupLevelButtonListeners()
    {
        if (levelButtons == null) return;

        for (int i = 0; i < levelButtons.Length; i++)
        {
            Button btn = levelButtons[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            int levelIndex = i + 1; // 1부터 시작
            btn.onClick.AddListener(() => OnClickLevel(levelIndex));
        }
    }

    /// <summary> 게임 타입 버튼 및 뒤로가기 버튼 리스너 등록. </summary>
    private void SetupTypeButtonListeners()
    {
        if (buttonTypeGuessNumber != null)
        {
            buttonTypeGuessNumber.onClick.RemoveAllListeners();
            buttonTypeGuessNumber.onClick.AddListener(() => OnClickGameType(GameType.GuessNumber));
        }

        if (buttonTypeCalculateNumber != null)
        {
            buttonTypeCalculateNumber.onClick.RemoveAllListeners();
            buttonTypeCalculateNumber.onClick.AddListener(() => OnClickGameType(GameType.CalculateNumber));
        }

        if (buttonTypeNumberSystem != null)
        {
            buttonTypeNumberSystem.onClick.RemoveAllListeners();
            buttonTypeNumberSystem.onClick.AddListener(() => OnClickGameType(GameType.NumberSystem));
        }
        if (buttonBack != null)
        {
            buttonBack.onClick.RemoveAllListeners();
            buttonBack.onClick.AddListener(OnClickBack);
        }
    }

    /// <summary> 1페이지(레벨 선택) 표시. </summary>
    private void ShowPageLevel()
    {
        if (pageLevel != null) pageLevel.SetActive(true);
        if (pageType != null) pageType.SetActive(false);
    }

    /// <summary> 2페이지(게임 타입 선택) 표시. </summary>
    private void ShowPageType()
    {
        if (pageLevel != null) pageLevel.SetActive(false);
        if (pageType != null) pageType.SetActive(true);
    }

    /// <summary> 
    /// 레벨 버튼 클릭 핸들러. 
    /// 선택된 레벨 저장 후 2페이지로 전환하며 UI 갱신.
    /// </summary>
    private void OnClickLevel(int level)
    {   
        SoundManager.Instance?.PlaySFX("Button");
        _selectedLevel = level;
        LevelSelectContext.SelectedLevel = level;

        // UI 갱신 (타이틀 이미지, 버튼 이미지, 텍스트 그라데이션)
        ApplySelectedLevelImage(level);
        ApplyGameTypeImages(level);
        ApplyPage2TextGradient(level);

        Debug.Log($"[LevelManager] Player Clicked Level: {level}");
        ShowPageType();
    }

    /// <summary> 상단 타이틀 이미지 갱신 ("Level N Game"). </summary>
    private void ApplySelectedLevelImage(int level)
    {
        if (selectedLevelImage == null || managerSetting?.gameLevelImages == null) return;
        ApplyImageFromSetting(selectedLevelImage, managerSetting.gameLevelImages, level);
    }

    /// <summary> 레벨에 따른 게임 타입 버튼 이미지 교체. </summary>
    private void ApplyGameTypeImages(int level)
    {
        if (buttonTypeGuessNumber != null && managerSetting.guessNumberLevelImages != null)
            ApplyImageFromSetting(buttonTypeGuessNumber.gameObject, managerSetting.guessNumberLevelImages, level);

        if (buttonTypeCalculateNumber != null && managerSetting.calculateNumberLevelImages != null)
            ApplyImageFromSetting(buttonTypeCalculateNumber.gameObject, managerSetting.calculateNumberLevelImages, level);

        if (buttonTypeNumberSystem != null && managerSetting.numberSystemLevelImages != null)
            ApplyImageFromSetting(buttonTypeNumberSystem.gameObject, managerSetting.numberSystemLevelImages, level);
    }

    /// <summary> 설정 배열에서 레벨에 맞는 이미지를 찾아 적용하는 헬퍼. </summary>
    private void ApplyImageFromSetting(GameObject targetObj, ImageSetting[] settings, int level)
    {
        int index = level - 1; // 레벨 1 -> 인덱스 0
        if (index < 0 || index >= settings.Length) return;
        
        ImageSetting imgSetting = settings[index];
        if (imgSetting != null)
        {
            ui.SetImageObj(targetObj, imgSetting);
        }
    }
    
    /// <summary> 2페이지 안내 텍스트 그라데이션 적용. </summary>
    private void ApplyPage2TextGradient(int level)
    {
        if (page2InfoTextObj == null) return;
        if (managerSetting.levelGradients == null) return;

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
    
    /// <summary> 뒤로가기 버튼 핸들러. </summary>
    private void OnClickBack()
    {   
        SoundManager.Instance?.PlaySFX("Button");
        Debug.Log($"[LevelManager] Player Clicked Back");
        ShowPageLevel();
    }

    /// <summary> 게임 타입 버튼 핸들러. 씬 전환 시작. </summary>
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

    /// <summary> 게임 씬 로드 비동기 처리 (페이드 아웃 -> 씬 이동). </summary>
    private async UniTask HandleLoadGameSceneAsync()
    {
        try
        {
            // 1. 페이드 아웃
            if (fader != null && fadeImage != null)
            {
                await fader.FadeOut(fadeImage, fadeTime, DestroyToken);
            }

            // 2. 씬 이름 결정
            string targetSceneName = "";
            switch (LevelSelectContext.SelectedGameType)
            {
                case GameType.GuessNumber:
                    targetSceneName = "GuessNumber";
                    break;
                case GameType.CalculateNumber:
                    targetSceneName = "CalculateNumber";
                    break;
                case GameType.NumberSystem:
                    targetSceneName = "NumberSystem";
                    break;
                default:
                    Debug.LogError($"[LevelManager] Unknown GameType: {LevelSelectContext.SelectedGameType}");
                    return;
            }

            // 3. 씬 로드
            if (string.IsNullOrEmpty(targetSceneName)) return;

            AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
            if (op == null) return;

            while (!op.isDone)
            {
                DestroyToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, DestroyToken);
            }
        }
        catch (OperationCanceledException)
        { 
            // 씬 전환 중 취소됨 (정상)
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] HandleLoadGameSceneAsync-> Exception: {e}");
        }
    }
}