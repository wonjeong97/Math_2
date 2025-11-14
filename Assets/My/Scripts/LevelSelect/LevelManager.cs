using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class LevelSetting
{
    public ImageSetting pageLevelBackground;
    public ButtonSetting[] levelButtons;

    public ImageSetting pageTypeBackground;
    public ImageSetting[] gameLevelImages;
    public ButtonSetting buttonTypeGuessNumber;
    public ButtonSetting buttonTypeCalculateNumber;
    public ButtonSetting buttonTypeNumberSystem;
    public ButtonSetting buttonBack;
}

public sealed class LevelManager : BaseManager<LevelSetting>
{
    [Header("Pages")]
    [SerializeField] private GameObject pageLevel;   // 1페이지 (레벨 선택)
    [SerializeField] private GameObject pageType;    // 2페이지 (타입 선택)

    [Header("Backgrounds")]
    [SerializeField] private GameObject pageLevelBackgroundObj; // 1페이지 배경 이미지 오브젝트
    [SerializeField] private GameObject pageTypeBackgroundObj;  // 2페이지 배경 이미지 오브젝트

    [Header("Level Buttons (1~N)")]
    [SerializeField] private Button[] levelButtons;  // 인덱스 0 -> Level 1, 1 -> Level 2 ...

    [Header("Type Buttons")]
    [SerializeField] private Button buttonTypeGuessNumber;
    [SerializeField] private Button buttonTypeCalculateNumber;
    [SerializeField] private Button buttonTypeNumberSystem;

    [Header("Page2 UI")]
    [SerializeField] private GameObject selectedLevelImage;
    [SerializeField] private Button buttonBack;

    [Header("Game Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    protected override string JsonPath => "JSON/LevelSetting.json";

    private int _selectedLevel = -1;

    /// <summary> 레벨 선택 씬을 초기화한다. JSON 설정을 적용해 UI를 구성하고, 버튼 리스너 및 페이지 상태를 설정한 뒤 페이드 인을 수행한다. </summary>
    protected override async UniTask Initialize()
    {
        try
        {
            ApplyBackgrounds();

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

    /// <summary> JSON 설정에 정의된 레벨 페이지 및 타입 페이지 배경 이미지를 적용한다. </summary>
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

    /// <summary> JSON 설정 기반으로 레벨 선택 버튼들의 UI(텍스트, 이미지, RectTransform 등)를 적용한다. </summary>
    private async UniTask SetupLevelButtonsUIAsync()
    {
        if (ui == null || managerSetting == null || managerSetting.levelButtons == null)
        {
            return;
        }

        if (levelButtons == null)
        {
            Debug.LogError("[LevelManager] SetupLevelButtonsUIAsync-> levelButtons is null");
            return;
        }

        int count = Mathf.Min(levelButtons.Length, managerSetting.levelButtons.Length);

        for (int i = 0; i < count; i++)
        {
            Button button = levelButtons[i];
            ButtonSetting setting = managerSetting.levelButtons[i];

            if (button == null || setting == null)
            {
                continue;
            }

            try
            {
                await ui.SetButtonObj(button.gameObject, setting, DestroyToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LevelManager] SetupLevelButtonsUIAsync-> Exception on index {i}: {e}");
            }
        }
    }

    /// <summary> JSON 설정 기반으로 게임 타입 선택 버튼, 뒤로가기 버튼의 UI를 적용한다. </summary>
    private async UniTask SetupTypeButtonsUIAsync()
    {
        if (ui == null || managerSetting == null)
        {
            return;
        }

        try
        {
            if (buttonTypeGuessNumber != null && managerSetting.buttonTypeGuessNumber != null)
            {
                await ui.SetButtonObj(buttonTypeGuessNumber.gameObject, managerSetting.buttonTypeGuessNumber, DestroyToken);
            }

            if (buttonTypeCalculateNumber != null && managerSetting.buttonTypeCalculateNumber != null)
            {
                await ui.SetButtonObj(buttonTypeCalculateNumber.gameObject, managerSetting.buttonTypeCalculateNumber, DestroyToken);
            }

            if (buttonTypeNumberSystem != null && managerSetting.buttonTypeNumberSystem != null)
            {
                await ui.SetButtonObj(buttonTypeNumberSystem.gameObject, managerSetting.buttonTypeNumberSystem, DestroyToken);
            }

            if (buttonBack != null && managerSetting.buttonBack != null)
            {
                await ui.SetButtonObj(buttonBack.gameObject, managerSetting.buttonBack, DestroyToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] SetupTypeButtonsUIAsync-> Exception: {e}");
        }
    }

    /// <summary> 레벨 선택 버튼에 클릭 리스너를 등록한다. 각 버튼은 자신의 인덱스를 레벨 번호(1부터 시작)로 전달한다. </summary>
    private void SetupLevelButtonListeners()
    {
        if (levelButtons == null) return;

        for (int i = 0; i < levelButtons.Length; i++)
        {
            Button btn = levelButtons[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            int levelIndex = i + 1; // 0 -> Level 1, 1 -> Level 2 ...
            btn.onClick.AddListener(() => OnClickLevel(levelIndex));
        }
    }

    /// <summary> 게임 타입 선택 버튼과 뒤로 가기 버튼에 클릭 리스너를 등록한다. </summary>
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

    /// <summary> 레벨 선택 페이지를 표시하고, 타입 선택 페이지는 숨긴다. </summary>
    private void ShowPageLevel()
    {
        if (pageLevel != null) pageLevel.SetActive(true);
        if (pageType != null) pageType.SetActive(false);
    }

    /// <summary> 타입 선택 페이지를 표시하고, 레벨 선택 페이지는 숨긴다. </summary>
    private void ShowPageType()
    {
        if (pageLevel != null) pageLevel.SetActive(false);
        if (pageType != null) pageType.SetActive(true);
    }

    /// <summary> 특정 레벨 버튼이 클릭되었을 때 호출된다. 선택된 레벨을 저장하고 상단 이미지와 페이지를 갱신한다. </summary>
    private void OnClickLevel(int level)
    {
        _selectedLevel = level;
        LevelSelectContext.SelectedLevel = level;

        ApplySelectedLevelImage(level);

        ShowPageType();
    }

    /// <summary> 선택된 레벨에 해당하는 이미지를 gameLevelImages 배열에서 찾아 상단 이미지에 적용한다. </summary>
    private void ApplySelectedLevelImage(int level)
    {
        if (selectedLevelImage == null)
        {
            Debug.LogError("[LevelManager] ApplySelectedLevelImage-> selectedLevelImage is null");
            return;
        }

        if (managerSetting == null || managerSetting.gameLevelImages == null)
        {
            Debug.LogError("[LevelManager] ApplySelectedLevelImage-> gameLevelImages is null");
            return;
        }

        int index = level - 1;
        if (index < 0 || index >= managerSetting.gameLevelImages.Length)
        {
            Debug.LogError($"[LevelManager] ApplySelectedLevelImage-> invalid index {index}");
            return;
        }

        ImageSetting imageSetting = managerSetting.gameLevelImages[index];
        if (imageSetting == null)
        {
            Debug.LogError($"[LevelManager] ApplySelectedLevelImage-> imageSetting is null at index {index}");
            return;
        }

        ui.SetImageObj(selectedLevelImage, imageSetting);
    }
    
    /// <summary> 타입 선택 페이지에서 뒤로 가기 버튼이 클릭되었을 때 호출된다. 레벨 선택 페이지로 돌아간다. </summary>
    private void OnClickBack()
    {
        ShowPageLevel();
    }

    /// <summary> 특정 게임 타입 버튼이 클릭되었을 때 호출, 선택된 타입을 저장하고, 유효한 레벨이 선택된 경우 게임 씬 로드를 시작. </summary>
    private void OnClickGameType(GameType type)
    {
        if (_selectedLevel <= 0)
        {
            Debug.LogError("[LevelManager] OnClickGameType-> 레벨이 선택되지 않았습니다.");
            return;
        }

        LevelSelectContext.SelectedGameType = type;
        HandleLoadGameSceneAsync().Forget();
    }

    /// <summary> 페이드 아웃을 수행한 뒤 지정된 게임 씬으로 전환, 씬 로드 도중 취소 토큰 확인. </summary>
    private async UniTask HandleLoadGameSceneAsync()
    {
        try
        {
            if (fader != null && fadeImage != null)
            {
                await fader.FadeOut(fadeImage, fadeTime, DestroyToken);
            }

            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogError("[LevelManager] HandleLoadGameSceneAsync-> gameSceneName is null or empty");
                return;
            }

            AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[LevelManager] HandleLoadGameSceneAsync-> LoadSceneAsync returned null");
                return;
            }

            while (!op.isDone)
            {
                DestroyToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, DestroyToken);
            }
        }
        catch (OperationCanceledException)
        { }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] HandleLoadGameSceneAsync-> Exception: {e}");
        }
    }
}
