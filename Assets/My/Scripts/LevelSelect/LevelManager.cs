using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class LevelSetting
{
    // 1페이지 (레벨 선택) 설정
    public ImageSetting pageLevelBackground;
    public TextSetting infoText;
    public ButtonSetting[] levelButtons;

    // 2페이지 (게임 타입 선택) 설정
    public ImageSetting pageTypeBackground;
    public ImageSetting[] gameLevelImages; // 상단에 표시될 "레벨 N 게임" 이미지 배열
    
    // 2페이지 안내 텍스트 (위치, 폰트 등 기본 설정)
    public TextSetting page2InfoText; 
    
    // 레벨별 텍스트 그라데이션 색상 (인덱스 0 = 레벨 1)
    public GradientData[] levelGradients;

    // 기본 버튼 설정 (위치/크기 초기화용)
    public ButtonSetting buttonTypeGuessNumber;
    public ButtonSetting buttonTypeCalculateNumber;
    public ButtonSetting buttonTypeNumberSystem;
    
    // 레벨별 게임 타입 버튼 이미지 배열 (인덱스 0 = 레벨 1)
    public ImageSetting[] guessNumberLevelImages;     // 수 맞추기 버튼 이미지들
    public ImageSetting[] calculateNumberLevelImages; // 계산하기 버튼 이미지들
    public ImageSetting[] numberSystemLevelImages;    // 수의 체계 버튼 이미지들

    public ButtonSetting buttonBack;
}

public sealed class LevelManager : BaseManager<LevelSetting>
{
    [Header("Pages")]
    [SerializeField] private GameObject pageLevel;   // 1페이지 (레벨 선택)
    [SerializeField] private GameObject pageType;    // 2페이지 (타입 선택)

    [Header("Backgrounds")]
    [SerializeField] private GameObject pageLevelBackgroundObj; // 1페이지 배경
    [SerializeField] private GameObject pageTypeBackgroundObj;  // 2페이지 배경

    [Header("Level Buttons (1~N)")]
    [SerializeField] private Button[] levelButtons;  // 인덱스 0 -> Level 1

    [Header("Type Buttons")]
    [SerializeField] private Button buttonTypeGuessNumber;
    [SerializeField] private Button buttonTypeCalculateNumber;
    [SerializeField] private Button buttonTypeNumberSystem;

    [Header("Page2 UI")]
    [SerializeField] private GameObject selectedLevelImage; // 상단 "레벨 N 게임" 이미지
    [SerializeField] private Button buttonBack;

    [Header("Text")] 
    [SerializeField] private GameObject page1InfoText;
    [SerializeField] private GameObject page2InfoTextObj;

    protected override string JsonPath => "JSON/LevelSetting.json";

    private int _selectedLevel = -1;

    /// <summary> 초기화: UI 설정 적용 및 리스너 등록 후 1페이지 표시 </summary>
    protected override async UniTask Initialize()
    {
        try
        {
            ApplyBackgrounds();
            
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

    private void ApplyBackgrounds()
    {
        if (ui == null || managerSetting == null) return;

        if (pageLevelBackgroundObj != null && managerSetting.pageLevelBackground != null)
        {
            ui.SetImageObj(pageLevelBackgroundObj, managerSetting.pageLevelBackground);
        }

        if (pageTypeBackgroundObj != null && managerSetting.pageTypeBackground != null)
        {
            ui.SetImageObj(pageTypeBackgroundObj, managerSetting.pageTypeBackground);
        }
    }

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

    private async UniTask SetupTypeButtonsUIAsync()
    {
        if (ui == null || managerSetting == null) return;

        try
        {
            // 기본 설정(위치, 크기 등) 적용
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

    /// <summary> 레벨 버튼 클릭 시 호출: 선택된 레벨을 저장하고 2페이지 UI를 갱신한다. </summary>
    private void OnClickLevel(int level)
    {
        _selectedLevel = level;
        LevelSelectContext.SelectedLevel = level;

        // 1. 상단 타이틀 이미지 갱신 ("레벨 N 게임")
        ApplySelectedLevelImage(level);
        
        // 2. 게임 타입 버튼 이미지 갱신 (레벨별 이미지 적용)
        ApplyGameTypeImages(level);
        
        // 3. "게임을 선택하세요!" 텍스트 그라데이션 변경
        ApplyPage2TextGradient(level);

        ShowPageType();
    }

    private void ApplySelectedLevelImage(int level)
    {
        if (selectedLevelImage == null || managerSetting?.gameLevelImages == null) return;
        ApplyImageFromSetting(selectedLevelImage, managerSetting.gameLevelImages, level);
    }

    /// <summary> [추가됨] 레벨에 따라 3개 게임 타입 버튼의 이미지를 교체한다. </summary>
    private void ApplyGameTypeImages(int level)
    {
        // Guess Number 버튼
        if (buttonTypeGuessNumber != null && managerSetting.guessNumberLevelImages != null)
        {
            ApplyImageFromSetting(buttonTypeGuessNumber.gameObject, managerSetting.guessNumberLevelImages, level);
        }

        // Calculate Number 버튼
        if (buttonTypeCalculateNumber != null && managerSetting.calculateNumberLevelImages != null)
        {
            ApplyImageFromSetting(buttonTypeCalculateNumber.gameObject, managerSetting.calculateNumberLevelImages, level);
        }

        // Number System 버튼
        if (buttonTypeNumberSystem != null && managerSetting.numberSystemLevelImages != null)
        {
            ApplyImageFromSetting(buttonTypeNumberSystem.gameObject, managerSetting.numberSystemLevelImages, level);
        }
    }

    /// <summary> 이미지 설정 배열에서 해당 레벨(인덱스)의 설정을 가져와 적용하는 헬퍼 메서드 </summary>
    private void ApplyImageFromSetting(GameObject targetObj, ImageSetting[] settings, int level)
    {
        int index = level - 1; // 레벨 1 -> 인덱스 0
        if (index < 0 || index >= settings.Length)
        {
            // Debug.LogWarning($"[LevelManager] Level {level} is out of range for image settings.");
            return;
        }

        ImageSetting imgSetting = settings[index];
        if (imgSetting != null)
        {
            // UIManager를 이용해 이미지 교체 (SourceImage, Color, RectTransform 등 반영)
            ui.SetImageObj(targetObj, imgSetting);
        }
    }
    
    /// <summary> 레벨별 그라데이션 적용 메서드 </summary>
    private void ApplyPage2TextGradient(int level)
    {
        if (page2InfoTextObj == null) return;
        if (managerSetting.levelGradients == null) return;

        int index = level - 1;
        if (index < 0 || index >= managerSetting.levelGradients.Length) return;

        GradientData data = managerSetting.levelGradients[index];
        
        // UIManager의 유틸리티를 사용해 컴포넌트 가져오기
        var gradientComp = UIManager.GetOrAdd<TextGlobalGradient>(page2InfoTextObj);
        
        if (gradientComp != null)
        {
            gradientComp.enabled = true; // 컴포넌트 활성화
            gradientComp.SetGradient(data.topLeft, data.topRight, data.bottomLeft, data.bottomRight);
        }
    }
    
    private void OnClickBack()
    {
        ShowPageLevel();
    }

    private void OnClickGameType(GameType type)
    {
        if (_selectedLevel <= 0)
        {
            Debug.LogError("[LevelManager] OnClickGameType-> Level is not selected.");
            return;
        }

        LevelSelectContext.SelectedGameType = type;
        HandleLoadGameSceneAsync().Forget();
    }

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