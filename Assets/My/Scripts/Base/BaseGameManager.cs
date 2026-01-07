using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// 미니게임 매니저의 공통 부모 클래스.
/// 공통 UI, 게임 흐름(정답/오답/종료), 초기화 로직을 관리함.
/// </summary>
public abstract class BaseGameManager<TSetting, TQuestion> : BaseManager<TSetting>
    where TSetting : class, IGameCommonSetting 
    where TQuestion : class
{
    [Header("--- Base UI References ---")]
    [SerializeField] protected Image levelImage;        
    [SerializeField] protected Image gameTypeImage;     
    [SerializeField] protected Image progressImage;     
    [SerializeField] protected Button backButton;       

    [Header("--- Base Question UI ---")]
    [SerializeField] protected TextMeshProUGUI questionTextObj; 
    [SerializeField] protected Image questionImageObj;          

    [Header("--- Base Buttons & Areas ---")]
    [SerializeField] protected GameObject[] answerButtons;      
    [SerializeField] protected RectTransform leftAreaRect;      
    [SerializeField] protected RectTransform rightAreaRect;     

    [Header("--- Base Result UI ---")]
    [SerializeField] protected GameObject pageCorrect;  
    [SerializeField] protected Image imageCorrect;      
    [SerializeField] protected GameObject pageWrong;    
    [SerializeField] protected Image imageWrong;        
    [SerializeField] protected Button buttonRetry;      
    [SerializeField] protected Button buttonGameEnd;    

    // --- Data & State ---
    protected List<TQuestion> currentLevelQuestions;    // 현재 레벨의 문제 목록
    protected int currentQuestionIndex;                 // 현재 진행 중인 문제 인덱스
    protected int totalQuestions = 4;                   // 총 문제 수
    protected TQuestion currentQuestion;                // 현재 문제 데이터
    protected bool isProcessing;                        // 중복 입력 방지 플래그

    private Sprite _defaultButtonSprite;
    private Color _defaultButtonColor;
    private Vector2 _defaultButtonSize;

    // --- Abstract Methods ---
    protected abstract int GetQuestionLevel(TQuestion question);
    protected abstract void SetupSpecificQuestionUI(TQuestion question); 
    protected abstract void SetupAnswerButtons(TQuestion question); 
    protected virtual bool EnableButtonWordWrapping => false;

    /// <summary>
    /// 게임 매니저 초기화 진입점.
    /// UI 설정, 버튼 스타일 적용, 게임 시작 로직을 실행함.
    /// </summary>
    protected override async UniTask Initialize()
    {
        OnSetupChildComponents();

        ApplyCommonUISettings();
        ApplyButtonStyles();

        if (pageCorrect) pageCorrect.SetActive(false);
        if (pageWrong) pageWrong.SetActive(false);
        if (questionImageObj) questionImageObj.gameObject.SetActive(false);

        if (buttonRetry) { buttonRetry.onClick.RemoveAllListeners(); buttonRetry.onClick.AddListener(OnRetryClicked); }
        if (buttonGameEnd) { buttonGameEnd.onClick.RemoveAllListeners(); buttonGameEnd.onClick.AddListener(OnGameEndClicked); }
        if (backButton) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => SceneManager.LoadScene(GameConstants.Scene.LevelSelect)); }

        CaptureDefaultButtonState();

        if (managerSetting == null)
        {
            Debug.LogError($"[{GetType().Name}] Data Load Failed.");
            return;
        }

        await OnGameInitializeAsync();

        ApplyButtonGradients(LevelSelectContext.SelectedLevel);
        StartGameLogic();
        
        if (fader != null && fadeImage != null)
        {
            await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
        }
    }
    
    /// <summary> 자식 클래스에서 컴포넌트 할당 등을 수행할 때 사용함. </summary>
    protected virtual void OnSetupChildComponents() { }
    
    /// <summary> 자식 클래스에서 추가적인 비동기 초기화가 필요할 때 사용함. </summary>
    protected virtual async UniTask OnGameInitializeAsync() { await UniTask.CompletedTask; }
    
    /// <summary> 게임 로직을 시작함. (문제 필터링 등) </summary>
    protected virtual void StartGameLogic() { }
    
    /// <summary> 지정된 인덱스의 문제로 UI를 설정함. </summary>
    protected void SetQuestionBase(int index)
    {
        if (currentLevelQuestions == null || index >= currentLevelQuestions.Count) return;

        isProcessing = false;
        currentQuestionIndex = index;
        currentQuestion = currentLevelQuestions[index];

        if (questionImageObj) questionImageObj.gameObject.SetActive(false);

        SetupSpecificQuestionUI(currentQuestion);
        SetupAnswerButtons(currentQuestion);
    }

    /// <summary> 버튼의 스타일(이미지, 색상, 크기, 상태)을 기본값으로 복구함. </summary>
    protected void RestoreButtonDefault(Button btn, Image btnImage, RectTransform btnRect = null)
    {
        if (btnImage && _defaultButtonSprite) 
        { 
            btnImage.sprite = _defaultButtonSprite; 
            btnImage.color = _defaultButtonColor; 
        }
        
        if (btnRect != null && _defaultButtonSize != Vector2.zero)
        {
            btnRect.sizeDelta = _defaultButtonSize;
        }

        ImageGlobalGradient gradient = btn.GetComponent<ImageGlobalGradient>();
        if (gradient) gradient.enabled = true;

        if (btn) 
        { 
            btn.transition = Selectable.Transition.ColorTint; 
            btn.spriteState = default; 
        }
    }

    /// <summary> 진행도 이미지를 업데이트함. </summary>
    protected void UpdateProgressImage(int level, int index)
    {
        if (!progressImage || managerSetting?.levelProgresses == null) return;
        int lvIdx = level - 1;
        if (lvIdx >= 0 && lvIdx < managerSetting.levelProgresses.Length)
        {
            var steps = managerSetting.levelProgresses[lvIdx].steps;
            if (index >= 0 && index < steps.Length)
            {
                UIManager.Instance.SetImageObj(progressImage.gameObject, steps[index], this.GetCancellationTokenOnDestroy()).Forget();
                progressImage.gameObject.SetActive(true);
            }
        }
    }
    
    /// <summary> 이미지 페이드 효과를 비동기 처리함. </summary>
    protected async UniTaskVoid HandleImageFadeAsync(Image target, ImageSetting setting, CancellationToken token)
    {
        float duration = setting.fadeDuration > 0 ? setting.fadeDuration : 1f;
        float startAlpha = setting.isFadeOut ? 1f : 0f;
        float endAlpha = setting.isFadeOut ? 0f : 1f;
        SetAlpha(target, startAlpha);
        do {
            await FadeAlpha(target, startAlpha, endAlpha, duration, token);
            if (setting.loop) await FadeAlpha(target, endAlpha, startAlpha, duration, token);
            else break;
        } while (setting.loop && target != null);
    }
    
    /// <summary> 알파값을 선형 보간함 (Lerp). </summary>
    private async UniTask FadeAlpha(Image target, float from, float to, float duration, CancellationToken token)
    {
        float time = 0f;
        while (time < duration) {
            token.ThrowIfCancellationRequested();
            if (target == null) return;
            time += Time.deltaTime;
            float t = time / duration;
            SetAlpha(target, Mathf.Lerp(from, to, t));
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        SetAlpha(target, to);
    }

    private void SetAlpha(Image target, float alpha)
    {
        if (target)
        {
            Color c = target.color;
            c.a = alpha;
            target.color = c;
        }
    }
    
    /// <summary> 초기화 시 버튼의 기본 상태(스프라이트, 색상, 크기)를 저장함. </summary>
    private void CaptureDefaultButtonState()
    {
        if (answerButtons is { Length: > 0 })
        {
            Image img = answerButtons[0].GetComponent<Image>();
            RectTransform rt = answerButtons[0].GetComponent<RectTransform>();
            if (img) { _defaultButtonSprite = img.sprite; _defaultButtonColor = img.color; }
            if (rt) _defaultButtonSize = rt.sizeDelta;
        }
    }
    
    /// <summary> 공통 UI(뒤로가기, 상단 이미지 등) 설정을 적용함. </summary>
    private void ApplyCommonUISettings()
    {
        if (managerSetting == null || ui == null) return;
        
        if (backButton && managerSetting.backButton != null) ui.SetButtonObj(backButton.gameObject, managerSetting.backButton).Forget();
        if (imageCorrect && managerSetting.correctImage != null) ui.SetImageObj(imageCorrect.gameObject, managerSetting.correctImage).Forget();
        if (imageWrong && managerSetting.wrongImage != null) ui.SetImageObj(imageWrong.gameObject, managerSetting.wrongImage).Forget();
        if (buttonRetry && managerSetting.retryButton != null) ui.SetButtonObj(buttonRetry.gameObject, managerSetting.retryButton).Forget();
        if (buttonGameEnd && managerSetting.gameEndButton != null) ui.SetButtonObj(buttonGameEnd.gameObject, managerSetting.gameEndButton).Forget();

        int levelIndex = LevelSelectContext.SelectedLevel - 1;
        if (levelIndex >= 0)
        {
            if (levelImage && managerSetting.levelImages != null && levelIndex < managerSetting.levelImages.Length)
            {
                ui.SetImageObj(levelImage.gameObject, managerSetting.levelImages[levelIndex]).Forget();
                levelImage.gameObject.SetActive(true);
            }
            if (gameTypeImage && managerSetting.gameTypeImages != null && levelIndex < managerSetting.gameTypeImages.Length)
            {
                ui.SetImageObj(gameTypeImage.gameObject, managerSetting.gameTypeImages[levelIndex]).Forget();
                gameTypeImage.gameObject.SetActive(true);
            }
        }
        
        if (JsonLoader.Instance != null && JsonLoader.Instance.settings != null)
        {
            TextSetting qTextSetting = JsonLoader.Instance.settings.gameQuestionText;
            if (questionTextObj != null && qTextSetting != null)
            {
                ui.SetTextObj(questionTextObj.gameObject, qTextSetting).Forget();
            }
        }
    }
    
    /// <summary> 전역 설정(Settings.json)을 통해 버튼 스타일을 적용함. </summary>
    private void ApplyButtonStyles()
    {
        if (JsonLoader.Instance == null || UIManager.Instance == null || answerButtons == null) return;
        Settings globalSettings = JsonLoader.Instance.settings;
        
        if (globalSettings != null && globalSettings.questionButtons != null && globalSettings.questionButtons.Length > 0)
        {
            int levelIndex = Mathf.Clamp(LevelSelectContext.SelectedLevel - 1, 0, globalSettings.questionButtons.Length - 1);
            ButtonSetting targetSetting = globalSettings.questionButtons[levelIndex];

            foreach (GameObject btn in answerButtons)
            {
                if (btn == null) continue;
                
                UIManager.Instance.SetButtonObj(btn, targetSetting).Forget();
                
                if (targetSetting.buttonText != null)
                {
                    var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp)
                    {
                        tmp.enableAutoSizing = true;
                        tmp.fontSizeMax = targetSetting.buttonText.fontSize;
                        tmp.fontSizeMin = targetSetting.buttonText.fontSize * 0.4f;
                        tmp.enableWordWrapping = EnableButtonWordWrapping;
                    }
                }
            }
        }
    }
    
    /// <summary> StreamingAssets에서 스프라이트를 로드함. </summary>
    protected Sprite LoadSpriteFromStreamingAssets(string fileName)
    {
        if (UIManager.Instance == null) return null;
        return UIManager.Instance.LoadSprite(fileName);
    }
    
    /// <summary> 정답 처리를 수행함 (비동기 호출 래퍼). </summary>
    protected virtual void HandleCorrectAnswer() { HandleCorrectAnswerAsync().Forget(); }

    private async UniTaskVoid HandleCorrectAnswerAsync()
    {   
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Correct ({currentQuestionIndex + 1}/{totalQuestions})");
        isProcessing = true;
        if (pageCorrect) pageCorrect.SetActive(true);
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(GameConstants.Sound.Correct);    
        }
        
        await UniTask.Delay(TimeSpan.FromSeconds(2));
        currentQuestionIndex++;
        if (currentQuestionIndex >= totalQuestions) 
        {
            OnGameEndClicked();
        }
        else
        {
            if (pageCorrect) pageCorrect.SetActive(false);
            SetQuestionBase(currentQuestionIndex);
        }
    }
    
    /// <summary> 오답 처리를 수행함. </summary>
    protected virtual void HandleWrongAnswer()
    {
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Wrong ({currentQuestionIndex + 1}/{totalQuestions})");
        isProcessing = true;    
        if (pageWrong) pageWrong.SetActive(true);
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(GameConstants.Sound.Wrong);    
        }
    }
    
    /// <summary> 다시하기 버튼 클릭을 처리함. </summary>
    protected virtual void OnRetryClicked()
    {   
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Retry ({currentQuestionIndex + 1}/{totalQuestions})");
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(GameConstants.Sound.ButtonClick);    
        }
        
        if (pageWrong) pageWrong.SetActive(false);
        SetQuestionBase(currentQuestionIndex);
    }
    
    /// <summary> 게임 종료 버튼 클릭을 처리함 (비동기 래퍼). </summary>
    protected virtual void OnGameEndClicked() { HandleGameEndAsync().Forget(); }
    
    private async UniTaskVoid HandleGameEndAsync()
    {
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Game End");
        
        if (fader != null && fadeImage != null) await fader.FadeOut(fadeImage, fadeTime, DestroyToken);
        GameResultContext.CorrectCount = currentQuestionIndex;
        SceneManager.LoadScene(GameConstants.Scene.GameEnd);
    }
    
    /// <summary> 레벨별 버튼/텍스트 그라데이션을 적용함. </summary>
    private void ApplyButtonGradients(int level)
    {
        if (JsonLoader.Instance == null) return;
        LevelSetting levelSetting = JsonLoader.Instance.LoadJsonData<LevelSetting>(GameConstants.Path.JsonLevelSetting);
        if (levelSetting == null || levelSetting.levelGradients == null) return;
        
        int index = level - 1;
        if (index < 0 || index >= levelSetting.levelGradients.Length) return;
        
        GradientData data = levelSetting.levelGradients[index];
        
        if (questionTextObj) ApplyGradientToTarget(questionTextObj, data);
        
        if (answerButtons != null) {
            foreach (var btnObj in answerButtons) {
                if (!btnObj) continue;
                
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                ApplyGradientToTarget(tmp, data);
                
                var imgGradient = btnObj.GetComponent<ImageGlobalGradient>();
                if (imgGradient) imgGradient.enabled = false;
            }
        }
    }
    
    /// <summary> 텍스트 그라데이션을 적용함. </summary>
    private void ApplyGradientToTarget(TextMeshProUGUI tmp, GradientData data)
    {
        if (!tmp || data == null) return;
        tmp.enableVertexGradient = false;
        tmp.color = Color.white;
        TextGlobalGradient gradient = UIManager.GetOrAdd<TextGlobalGradient>(tmp.gameObject);
        if (gradient) {
            gradient.SetGradient(data.topLeft, data.topRight, data.bottomLeft, data.bottomRight);
            gradient.enabled = true;
            gradient.ApplyGradient();
        }
    }

    protected void OnDestroy()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(null);    
        }
    }
}