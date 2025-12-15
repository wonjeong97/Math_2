using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Video;
using Random = UnityEngine.Random;

public class CalculateNumberManager : BaseGameManager<CalculateNumberSetting, CalculateNumberQuestion>
{
    [Header("--- Calculate Specific ---")]
    [SerializeField] private RectTransform questionImageRoot; 
    [SerializeField] private GameObject questionImagePrefab;
    [SerializeField] private Transform leftQuestionZone;
    [SerializeField] private Transform rightQuestionZone;

    // 진행 상태 변수
    private int _currentSequenceIndex = 0;
    private int _foundAnswerCount = 0;

    protected override string GetJsonFileName() => "CalculateNumber.json";
    protected override int GetQuestionLevel(CalculateNumberQuestion question) => question.level;

    // 게임 시작 로직
    protected override void StartGameLogic()
    {
        int selectedLevel = LevelSelectContext.SelectedLevel > 0 ? LevelSelectContext.SelectedLevel : 1;
        
        if (currentSetting?.questions != null)
        {
            var levelQuestions = currentSetting.questions.Where(q => q.level == selectedLevel).ToList();
            if (levelQuestions.Count > 0)
            {
                int count = Mathf.Min(levelQuestions.Count, totalQuestions);
                currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
                totalQuestions = currentLevelQuestions.Count;
                SetQuestionBase(0);
            }
            else
            {
                Debug.LogWarning($"Level {selectedLevel} Problems Not Found");
            }
        }
    }

    // BaseGameManager의 Initialize 과정에서 UI 설정을 위해 오버라이드
    protected override void Initialize()
    {
        // 1. 데이터 로드
        LoadGameData();

        // 2. 자식 클래스 컴포넌트 캐싱
        if (questionVideoObject != null)
        {
            _questionRawImage = questionVideoObject.GetComponent<RawImage>();
            _questionVideoPlayer = questionVideoObject.GetComponent<VideoPlayer>();
            questionVideoObject.SetActive(false);
        }

        if (questionImageRoot != null)
        {
            foreach (Transform child in questionImageRoot) Destroy(child.gameObject);
            questionImageRoot.gameObject.SetActive(false);
        }

        // 3. 스타일 및 UI 세팅 적용
        ApplyUISettings();
        ApplyButtonStyles(); 

        // 4. 부모 초기화 실행
        base.Initialize();
    }

    // [중요] UI 세팅 적용 (Settings.json, Data Json 값 적용)
    private void ApplyUISettings()
    {
        if (currentSetting == null || UIManager.Instance == null) return;
        
        // BaseGameManager의 변수(protected) 사용
        this.buttonMargin = currentSetting.buttonMargin;

        if (backButton && currentSetting.backButton != null)
            UIManager.Instance.SetButtonObj(backButton.gameObject, currentSetting.backButton).Forget();

        if (imageCorrect != null && currentSetting.correctImage != null)
            UIManager.Instance.SetImageObj(imageCorrect.gameObject, currentSetting.correctImage);
        
        if (imageWrong != null && currentSetting.wrongImage != null)
            UIManager.Instance.SetImageObj(imageWrong.gameObject, currentSetting.wrongImage);
        
        if (buttonRetry != null && currentSetting.retryButton != null)
            UIManager.Instance.SetButtonObj(buttonRetry.gameObject, currentSetting.retryButton).Forget();
        
        if (buttonGameEnd != null && currentSetting.gameEndButton != null)
            UIManager.Instance.SetButtonObj(buttonGameEnd.gameObject, currentSetting.gameEndButton).Forget();
        
        // 상단 이미지 (레벨, 게임타입) 적용
        int levelIndex = LevelSelectContext.SelectedLevel - 1;
        if (levelIndex >= 0)
        {
            if (levelImage && currentSetting.levelImages != null && levelIndex < currentSetting.levelImages.Length)
            {
                UIManager.Instance.SetImageObj(levelImage.gameObject, currentSetting.levelImages[levelIndex]);
                levelImage.gameObject.SetActive(true);
            }
            if (gameTypeImage && currentSetting.gameTypeImages != null && levelIndex < currentSetting.gameTypeImages.Length)
            {
                UIManager.Instance.SetImageObj(gameTypeImage.gameObject, currentSetting.gameTypeImages[levelIndex]);
                gameTypeImage.gameObject.SetActive(true);
            }
        }
    }

    // 버튼 스타일 적용 (Settings.json의 questionButton 사용)
    private void ApplyButtonStyles()
    {
        if (JsonLoader.Instance != null && UIManager.Instance != null && answerButtons != null)
        {
            Settings globalSettings = JsonLoader.Instance.LoadJsonData<Settings>("Settings.json");
            if (globalSettings != null && globalSettings.questionButton != null)
            {
                foreach (GameObject btn in answerButtons)
                {
                    if(btn == null) continue;

                    // 공통 버튼 스타일 적용
                    UIManager.Instance.SetButtonObj(btn, globalSettings.questionButton).Forget();
                    
                    // 폰트 사이즈 오토 설정 등 추가 처리
                    if (globalSettings.questionButton.buttonText != null)
                    {
                        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                        if (tmp)
                        {
                            tmp.enableAutoSizing = true;
                            tmp.fontSizeMax = globalSettings.questionButton.buttonText.fontSize;
                            tmp.fontSizeMin = globalSettings.questionButton.buttonText.fontSize * 0.4f;
                        }
                    }
                }
            }
        }
    }

    protected override void SetupSpecificQuestionUI(CalculateNumberQuestion q)
    {
        _currentSequenceIndex = 0;
        _foundAnswerCount = 0;

        UpdateProgressImage(q.level, currentQuestionIndex);

        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform contentParent = isTextLeft ? rightQuestionZone : leftQuestionZone;

        if (questionTextObj && textParent)
        {
            questionTextObj.transform.SetParent(textParent, false);
            questionTextObj.text = q.questionText;
            questionTextObj.gameObject.SetActive(!string.IsNullOrEmpty(q.questionText));
        }

        if (questionImageRoot)
        {
            foreach (Transform child in questionImageRoot) Destroy(child.gameObject);
            questionImageRoot.gameObject.SetActive(false);
        }

        bool hasVideo = q.questionVideo != null && !string.IsNullOrEmpty(q.questionVideo.fileName);
        bool hasImages = q.questionImages != null && q.questionImages.Length > 0;

        if (hasVideo)
        {
            if (questionVideoObject && contentParent)
                questionVideoObject.transform.SetParent(contentParent, false);
            PlayVideo(q.questionVideo);
        }
        else if (hasImages)
        {
            if (questionImageRoot && questionImagePrefab && contentParent)
            {
                questionImageRoot.SetParent(contentParent, false);
                questionImageRoot.gameObject.SetActive(true);

                foreach (var imgSetting in q.questionImages)
                {
                    if (imgSetting == null) continue;
                    GameObject newImgObj = Instantiate(questionImagePrefab, questionImageRoot);
                    newImgObj.SetActive(true);

                    if (UIManager.Instance != null) UIManager.Instance.SetImageObj(newImgObj, imgSetting);

                    if (imgSetting.useFade)
                    {
                        Image imgComp = newImgObj.GetComponent<Image>();
                        if (imgComp) HandleImageFadeAsync(imgComp, imgSetting, newImgObj.GetCancellationTokenOnDestroy()).Forget();
                    }
                }
            }
        }
    }

    protected override void SetupAnswerButtons(CalculateNumberQuestion q)
    {
        List<string> options = new List<string>();
        if (q.correctAnswers != null) options.AddRange(q.correctAnswers);
        if (q.wrongAnswers != null)
        {
            int remaining = 4 - options.Count;
            if (remaining > 0) options.AddRange(q.wrongAnswers.Take(remaining));
        }
        
        options = options.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledBtns = answerButtons.OrderBy(x => Random.value).ToList();

        Sprite overridePressed = null;
        if (q.buttonStyleOverride != null && q.buttonStyleOverride.useOverride)
            overridePressed = LoadSpriteFromStreamingAssets(q.buttonStyleOverride.pressedImageName);

        List<GameObject> activeBtns = shuffledBtns.Take(options.Count).ToList();
        int half = Mathf.CeilToInt(activeBtns.Count / 2f);
        PlaceButtonsInArea(activeBtns.Take(half).ToList(), leftAreaRect);
        PlaceButtonsInArea(activeBtns.Skip(half).ToList(), rightAreaRect);

        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = shuffledBtns[i];
            Button btn = btnObj.GetComponent<Button>();
            Image btnImage = btnObj.GetComponent<Image>();
            
            btn.interactable = true;
            btn.onClick.RemoveAllListeners();

            if (i < options.Count)
            {
                string text = options[i];
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp) tmp.text = text;

                if (q.buttonStyleOverride != null && q.buttonStyleOverride.useOverride)
                {
                    ApplyButtonOverride(btn, btnImage, q.buttonStyleOverride);
                }
                else
                {
                    RestoreButtonDefault(btn, btnImage);
                }

                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj, overridePressed));
                btnObj.SetActive(true);
            }
            else
            {
                btnObj.SetActive(false);
            }
        }
    }

    private void OnAnswerClicked(string text, GameObject btnObj, Sprite pressedSprite)
    {
        if (isProcessing) return;
        bool isCorrect = false;
        bool isLevelClear = false;

        switch (currentQuestion.type)
        {
            case QuestionType.SingleChoice:
                if (currentQuestion.correctAnswers.Contains(text)) { isCorrect = true; isLevelClear = true; }
                break;
            case QuestionType.MultipleChoice: // Type 1 (별 세기 문제)
                if (currentQuestion.correctAnswers.Contains(text))
                {
                    isCorrect = true;
                    _foundAnswerCount++;

                    // 모든 정답(버튼)을 다 찾았는지 확인
                    if (_foundAnswerCount >= currentQuestion.correctAnswers.Length)
                    {
                        isLevelClear = true;
                    }
                }
                break;
        }

        if (isCorrect)
        {
            // 정답 시 버튼 이미지 교체
            if (pressedSprite && btnObj.TryGetComponent(out Image img)) 
            {
                img.sprite = pressedSprite;
            }
            
            // 중복 클릭 방지 (버튼 비활성화)
            btnObj.GetComponent<Button>().interactable = false;

            if (isLevelClear) 
            {
                HandleCorrectAnswer(); // 모든 버튼을 다 눌렀을 때 정답 처리
            }
        }
        else 
        {
            HandleWrongAnswer(); // 오답 처리
        }
    }

    // --- Helper Functions ---
    
    private async UniTaskVoid HandleImageFadeAsync(Image target, ImageSetting setting, CancellationToken token)
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
    private void SetAlpha(Image target, float alpha) { if(target) { Color c=target.color; c.a=alpha; target.color=c; } }

    private void RestoreButtonDefault(Button btn, Image btnImage)
    {
        // 1. 이미지 및 색상 복구
        if (btnImage && defaultButtonSprite)
        {
            btnImage.sprite = defaultButtonSprite;
            btnImage.color = defaultButtonColor;
        }
        
        // 2. 그라데이션 복구
        ImageGlobalGradient gradient = btn.GetComponent<ImageGlobalGradient>();
        if (gradient) gradient.enabled = true;
        
        // 3. 버튼 전환 모드 및 스프라이트 상태 복구
        if (btn) 
        {
            // SpriteSwap 모드에서 다시 기본 ColorTint 모드로 변경
            btn.transition = Selectable.Transition.ColorTint; 
            
            // SpriteState 초기화 (잔여 스프라이트 제거)
            btn.spriteState = default(SpriteState);
        }
    }

    private void ApplyButtonOverride(Button btn, Image btnImage, ButtonOverrideSetting set)
    {
        Sprite normal = LoadSpriteFromStreamingAssets(set.normalImageName);
        if(normal && btnImage) { btnImage.sprite = normal; btnImage.color = set.buttonColor; }
        ImageGlobalGradient gradient = btn.GetComponent<ImageGlobalGradient>();
        if (gradient) gradient.enabled = false;
        
        if (btn && !string.IsNullOrEmpty(set.pressedImageName))
        {
            Sprite pressed = LoadSpriteFromStreamingAssets(set.pressedImageName);
            if (pressed)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                SpriteState newState = new SpriteState { pressedSprite = pressed, highlightedSprite = normal, selectedSprite = normal, disabledSprite = pressed };
                btn.spriteState = newState;
            }
        }
    }
    
    private void UpdateProgressImage(int level, int index)
    {
        if (!progressImage || currentSetting?.levelProgresses == null) return;
        int lvIdx = level - 1;
        if(lvIdx >= 0 && lvIdx < currentSetting.levelProgresses.Length)
        {
            var steps = currentSetting.levelProgresses[lvIdx].steps;
            if(index >= 0 && index < steps.Length)
            {
                UIManager.Instance.SetImageObj(progressImage.gameObject, steps[index]);
                progressImage.gameObject.SetActive(true);
            }
        }
    }
}