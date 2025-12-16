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
/// 공통 UI, 게임 흐름(정답/오답/종료), 초기화 로직 관리.
/// </summary>
public abstract class BaseGameManager<TSetting, TQuestion> : BaseManager<TSetting>
    where TSetting : class, IGameCommonSetting 
    where TQuestion : class
{
    [Header("--- Base UI References ---")]
    [SerializeField] protected Image levelImage;        // 상단 레벨 표시 이미지
    [SerializeField] protected Image gameTypeImage;     // 상단 게임 타입 표시 이미지
    [SerializeField] protected Image progressImage;     // 진행도(게이지) 이미지
    [SerializeField] protected Button backButton;       // 뒤로가기 버튼

    [Header("--- Base Question UI ---")]
    [SerializeField] protected TextMeshProUGUI questionTextObj; // 문제 텍스트
    [SerializeField] protected Image questionImageObj;          // 문제 이미지

    [Header("--- Base Buttons & Areas ---")]
    [SerializeField] protected GameObject[] answerButtons;      // 정답 버튼 배열 (4개)
    [SerializeField] protected RectTransform leftAreaRect;      // 왼쪽 버튼 배치 영역
    [SerializeField] protected RectTransform rightAreaRect;     // 오른쪽 버튼 배치 영역
    private float buttonMargin = 20f;                           // 버튼 간 간격

    [Header("--- Base Result UI ---")]
    [SerializeField] protected GameObject pageCorrect;  // 정답 페이지
    [SerializeField] protected Image imageCorrect;      // 정답 이미지
    [SerializeField] protected GameObject pageWrong;    // 오답 페이지
    [SerializeField] protected Image imageWrong;        // 오답 이미지
    [SerializeField] protected Button buttonRetry;      // 다시하기 버튼
    [SerializeField] protected Button buttonGameEnd;    // 게임 종료 버튼

    // --- Data & State ---
    protected List<TQuestion> currentLevelQuestions;    // 현재 레벨의 문제 목록
    protected int currentQuestionIndex;                 // 현재 진행 중인 문제 인덱스
    protected int totalQuestions = 4;                   // 총 문제 수
    protected TQuestion currentQuestion;                // 현재 문제 데이터
    protected bool isProcessing;                        // 중복 입력 방지 플래그

    // 버튼 복구용 캐싱 데이터
    private Sprite defaultButtonSprite;
    private Color defaultButtonColor;
    private Vector2 defaultButtonSize;

    // --- Abstract Methods ---
    /// <summary> JSON 파일 이름 반환. </summary>
    protected abstract string GetJsonFileName();
    
    // JSON 경로 프로퍼티 구현
    protected override string JsonPath => $"JSON/{GetJsonFileName()}";
    
    /// <summary> 문제 데이터에서 레벨 반환. </summary>
    protected abstract int GetQuestionLevel(TQuestion question);
    
    /// <summary> 개별 문제 UI 설정 (텍스트, 이미지 등). </summary>
    protected abstract void SetupSpecificQuestionUI(TQuestion question); 
    
    /// <summary> 정답 버튼 배치 및 설정. </summary>
    protected abstract void SetupAnswerButtons(TQuestion question); 

    /// <summary> 버튼 텍스트 줄바꿈 허용 여부 (기본 false). </summary>
    protected virtual bool EnableButtonWordWrapping => false;

    /// <summary>
    /// 게임 매니저 초기화 진입점.
    /// UI 설정, 버튼 스타일 적용, 게임 시작 로직 실행.
    /// </summary>
    protected override async UniTask Initialize()
    {
        // 1. 자식 컴포넌트 설정 (Hook)
        OnSetupChildComponents();

        // 2. 공통 UI 및 버튼 스타일 적용
        ApplyCommonUISettings();
        ApplyButtonStyles();

        // 3. UI 리셋 및 리스너 등록
        if (pageCorrect) pageCorrect.SetActive(false);
        if (pageWrong) pageWrong.SetActive(false);
        if (questionImageObj) questionImageObj.gameObject.SetActive(false);

        if (buttonRetry) { buttonRetry.onClick.RemoveAllListeners(); buttonRetry.onClick.AddListener(OnRetryClicked); }
        if (buttonGameEnd) { buttonGameEnd.onClick.RemoveAllListeners(); buttonGameEnd.onClick.AddListener(OnGameEndClicked); }
        if (backButton) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => SceneManager.LoadScene("LevelSelect")); }

        // 4. 버튼 기본 상태 캡처 (스타일 적용 후)
        CaptureDefaultButtonState();

        if (managerSetting == null)
        {
            Debug.LogError($"[{GetType().Name}] Data Load Failed.");
            return;
        }

        // 5. 추가 비동기 초기화 (Hook)
        await OnGameInitializeAsync();

        // 6. 그라데이션 및 게임 시작
        ApplyButtonGradients(LevelSelectContext.SelectedLevel);
        StartGameLogic();
        
        // 7. 페이드 인
        if (fader != null && fadeImage != null)
        {
            await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
        }
    }
    
    /// <summary> 자식 클래스에서 컴포넌트 할당 등을 수행할 때 사용 (동기). </summary>
    protected virtual void OnSetupChildComponents() { }
    
    /// <summary> 자식 클래스에서 추가적인 비동기 초기화가 필요할 때 사용. </summary>
    protected virtual async UniTask OnGameInitializeAsync() { await UniTask.CompletedTask; }
    
    /// <summary> 게임 로직 시작 (문제 필터링 등). </summary>
    protected virtual void StartGameLogic() { }
    
    /// <summary> 지정된 인덱스의 문제로 UI 설정. </summary>
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

    // --- Common Utilities ---

    /// <summary> 버튼의 스타일(이미지, 색상, 크기, 상태)을 기본값으로 복구. </summary>
    protected void RestoreButtonDefault(Button btn, Image btnImage, RectTransform btnRect = null)
    {
        // 1. 이미지 및 색상
        if (btnImage && defaultButtonSprite) 
        { 
            btnImage.sprite = defaultButtonSprite; 
            btnImage.color = defaultButtonColor; 
        }
        
        // 2. 사이즈
        if (btnRect != null && defaultButtonSize != Vector2.zero)
        {
            btnRect.sizeDelta = defaultButtonSize;
        }

        // 3. 그라데이션
        ImageGlobalGradient gradient = btn.GetComponent<ImageGlobalGradient>();
        if (gradient) gradient.enabled = true;

        // 4. 버튼 상태
        if (btn) 
        { 
            btn.transition = Selectable.Transition.ColorTint; 
            btn.spriteState = default; 
        }
    }

    /// <summary> 진행도 이미지 업데이트. </summary>
    protected void UpdateProgressImage(int level, int index)
    {
        if (!progressImage || managerSetting?.levelProgresses == null) return;
        int lvIdx = level - 1;
        if (lvIdx >= 0 && lvIdx < managerSetting.levelProgresses.Length)
        {
            var steps = managerSetting.levelProgresses[lvIdx].steps;
            if (index >= 0 && index < steps.Length)
            {
                UIManager.Instance.SetImageObj(progressImage.gameObject, steps[index]);
                progressImage.gameObject.SetActive(true);
            }
        }
    }
    
    /// <summary> 이미지 페이드 효과 비동기 처리. </summary>
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
    
    /// <summary> 알파값 선형 보간 (Lerp). </summary>
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
    
    /// <summary> 초기화 시 버튼의 기본 상태(스프라이트, 색상, 크기) 저장. </summary>
    private void CaptureDefaultButtonState()
    {
        if (answerButtons is { Length: > 0 })
        {
            Image img = answerButtons[0].GetComponent<Image>();
            RectTransform rt = answerButtons[0].GetComponent<RectTransform>();
            if (img) { defaultButtonSprite = img.sprite; defaultButtonColor = img.color; }
            if (rt) defaultButtonSize = rt.sizeDelta;
        }
    }
    
    /// <summary> 공통 UI(뒤로가기, 상단 이미지 등) 설정 적용. </summary>
    private void ApplyCommonUISettings()
    {
        if (managerSetting == null || ui == null) return;
        this.buttonMargin = managerSetting.buttonMargin;
        if (backButton && managerSetting.backButton != null) ui.SetButtonObj(backButton.gameObject, managerSetting.backButton).Forget();
        if (imageCorrect && managerSetting.correctImage != null) ui.SetImageObj(imageCorrect.gameObject, managerSetting.correctImage);
        if (imageWrong && managerSetting.wrongImage != null) ui.SetImageObj(imageWrong.gameObject, managerSetting.wrongImage);
        if (buttonRetry && managerSetting.retryButton != null) ui.SetButtonObj(buttonRetry.gameObject, managerSetting.retryButton).Forget();
        if (buttonGameEnd && managerSetting.gameEndButton != null) ui.SetButtonObj(buttonGameEnd.gameObject, managerSetting.gameEndButton).Forget();

        int levelIndex = LevelSelectContext.SelectedLevel - 1;
        if (levelIndex >= 0)
        {
            if (levelImage && managerSetting.levelImages != null && levelIndex < managerSetting.levelImages.Length)
            {
                ui.SetImageObj(levelImage.gameObject, managerSetting.levelImages[levelIndex]);
                levelImage.gameObject.SetActive(true);
            }
            if (gameTypeImage && managerSetting.gameTypeImages != null && levelIndex < managerSetting.gameTypeImages.Length)
            {
                ui.SetImageObj(gameTypeImage.gameObject, managerSetting.gameTypeImages[levelIndex]);
                gameTypeImage.gameObject.SetActive(true);
            }
        }
    }
    
    /// <summary> 전역 설정(Settings.json)을 통한 버튼 스타일 적용. </summary>
    private void ApplyButtonStyles()
    {
        if (JsonLoader.Instance == null || UIManager.Instance == null || answerButtons == null) return;
        Settings globalSettings = JsonLoader.Instance.settings;
        if (globalSettings != null && globalSettings.questionButton != null)
        {
            foreach (GameObject btn in answerButtons)
            {
                if (btn == null) continue;
                UIManager.Instance.SetButtonObj(btn, globalSettings.questionButton).Forget();
                if (globalSettings.questionButton.buttonText != null)
                {
                    var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp)
                    {
                        tmp.enableAutoSizing = true;
                        tmp.fontSizeMax = globalSettings.questionButton.buttonText.fontSize;
                        tmp.fontSizeMin = globalSettings.questionButton.buttonText.fontSize * 0.4f;
                        tmp.enableWordWrapping = EnableButtonWordWrapping;
                    }
                }
            }
        }
    }
    
    /// <summary> StreamingAssets에서 스프라이트 로드. </summary>
    protected Sprite LoadSpriteFromStreamingAssets(string fileName)
    {
        if (UIManager.Instance == null) return null;
        return UIManager.Instance.LoadSprite(fileName);
    }
    
    /// <summary> 정답 처리 (비동기 호출 래퍼). </summary>
    protected virtual void HandleCorrectAnswer() { HandleCorrectAnswerAsync().Forget(); }

    private async UniTaskVoid HandleCorrectAnswerAsync()
    {   
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Correct ({currentQuestionIndex + 1}/{totalQuestions})");
        isProcessing = true;
        if (pageCorrect) pageCorrect.SetActive(true);
        await UniTask.Delay(TimeSpan.FromSeconds(2));
        currentQuestionIndex++;
        if (currentQuestionIndex >= totalQuestions) 
        {
            // 마지막 문제면 종료
            OnGameEndClicked();
        }
        else
        {
            // 다음 문제 진행
            if (pageCorrect) pageCorrect.SetActive(false);
            SetQuestionBase(currentQuestionIndex);
        }
    }
    
    /// <summary> 오답 처리 로직 (UI 표시). </summary>
    protected virtual void HandleWrongAnswer()
    {
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Wrong ({currentQuestionIndex + 1}/{totalQuestions})");
        isProcessing = true;    
        if (pageWrong) pageWrong.SetActive(true);
    }
    
    /// <summary> 다시하기 버튼 클릭 핸들러. </summary>
    protected virtual void OnRetryClicked()
    {   
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Retry ({currentQuestionIndex + 1}/{totalQuestions})");
        if (pageWrong) pageWrong.SetActive(false);
        SetQuestionBase(currentQuestionIndex);
    }
    
    /// <summary> 게임 종료 버튼 클릭 핸들러 (비동기 래퍼). </summary>
    protected virtual void OnGameEndClicked() { HandleGameEndAsync().Forget(); }
    
    /// <summary> 게임 종료 처리 (페이드 아웃 -> 결과 화면 이동). </summary>
    private async UniTaskVoid HandleGameEndAsync()
    {
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Game End");
        if (fader != null && fadeImage != null) await fader.FadeOut(fadeImage, fadeTime, DestroyToken);
        GameResultContext.CorrectCount = currentQuestionIndex;
        SceneManager.LoadScene("GameEnd");
    }
    
    /// <summary> 레벨별 버튼/텍스트 그라데이션 적용. </summary>
    private void ApplyButtonGradients(int level)
    {
        if (JsonLoader.Instance == null) return;
        LevelSetting levelSetting = JsonLoader.Instance.LoadJsonData<LevelSetting>("JSON/LevelSetting.json");
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
                Image btnImage = btnObj.GetComponent<Image>();
                ApplyGradientToImage(btnImage, data);
            }
        }
    }
    
    /// <summary> 이미지 그라데이션 적용. </summary>
    private void ApplyGradientToImage(Image targetImage, GradientData data)
    {
        if (!targetImage || data == null) return;
        targetImage.color = Color.white;
        ImageGlobalGradient gradient = UIManager.GetOrAdd<ImageGlobalGradient>(targetImage.gameObject);
        if (gradient) {
            Color[] colors = { data.topLeft, data.topRight, data.bottomRight, data.bottomLeft };
            int offset = Random.Range(0, 4);
            gradient.SetGradient(colors[(0 + offset) % 4], colors[(1 + offset) % 4], colors[(3 + offset) % 4], colors[(2 + offset) % 4]);
            gradient.enabled = true;
        }
    }
    
    /// <summary> 텍스트 그라데이션 적용. </summary>
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

    /// <summary> 지정된 영역 내에 버튼들을 랜덤하게 배치. </summary>
    protected void PlaceButtonsInArea(List<GameObject> buttonsToPlace, RectTransform areaRect)
    {   
        // 유효성 검사
        if (!areaRect || buttonsToPlace == null || buttonsToPlace.Count == 0) return;
        Rect rect = areaRect.rect;
        Vector2 halfAreaSize = rect.size * 0.5f;
        
        // 그리드 설정
        const int columns = 1;  // 열
        const int rows = 2;     // 행
        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;
        
        // 슬롯 좌표 생성
        List<Vector2> slots = new List<Vector2>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {   
                // 각 셀의 중심 좌표를 계산하여 리스트에 추가
                slots.Add(new Vector2(-halfAreaSize.x + cellWidth * (col + 0.5f), halfAreaSize.y - cellHeight * (row + 0.5f)));
            }
        }
        
        // 슬롯 랜덤 섞기
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1); 
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }
        
        // 버튼 배치 및 무작위 위치 적용
        int count = Mathf.Min(buttonsToPlace.Count, slots.Count);
        for (int i = 0; i < count; i++) {
            
            // 버튼 크기 계산
            GameObject obj = buttonsToPlace[i];
            RectTransform rt = obj.GetComponent<RectTransform>();
            Vector3 scale = rt.localScale;
            float w = rt.sizeDelta.x * scale.x;
            float h = rt.sizeDelta.y * scale.y;
            
            // Jitter 여유공간 계산
            // 그리드 크기에서 버튼 크기를 빼고, buttonMargin까지 뺀 남은 공간의 절반
            float jitterX = Mathf.Max(0f, (cellWidth - w) * 0.5f - buttonMargin);
            float jitterY = Mathf.Max(0f, (cellHeight - h) * 0.5f - buttonMargin);
            
            Vector2 basePos = slots[i]; // 섞어둔 슬롯의 중심 좌표
            
            // 중심 좌표를 기준으로 랜덤하게 위치를 살짝 비틈 (-jitter ~ +jitter)
            float offsetX = jitterX > 0 ? Random.Range(-jitterX, jitterX) : 0f;
            float offsetY = jitterY > 0 ? Random.Range(-jitterY, jitterY) : 0f;
            
            // 부모 설정 및 최종 위치 적용
            rt.SetParent(areaRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = basePos + new Vector2(offsetX, offsetY);
        }
    }
}