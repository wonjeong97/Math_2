using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Video;
using Random = UnityEngine.Random;

public class NumberSystemManager : BaseGameManager<NumberSystemSetting, NumberSystemQuestion>
{
    [Header("--- NumberSystem Specific ---")]
    [SerializeField] private Transform leftQuestionZone;
    [SerializeField] private Transform rightQuestionZone;

    // 진행 상태 변수 (Sequence, MultipleChoice용)
    private int _currentSequenceIndex = 0;
    private int _foundAnswerCount = 0;
    private HashSet<string> _foundAnswersSet;

    protected override string GetJsonFileName() => "NumberSystem.json";

    protected override int GetQuestionLevel(NumberSystemQuestion q) => q.level;

    // [중요 수정] 초기화 순서 제어
    protected override void Initialize()
    {
        // 1. 데이터 먼저 로드
        LoadGameData();

        // 2. 비디오 컴포넌트 등 자식 클래스 전용 초기화
        if (questionVideoObject != null)
        {
            _questionRawImage = questionVideoObject.GetComponent<RawImage>();
            _questionVideoPlayer = questionVideoObject.GetComponent<VideoPlayer>();
            questionVideoObject.SetActive(false);
        }

        // 3. UI 세팅 및 스타일 적용 (Base.Initialize보다 먼저 실행되어야 함)
        ApplyUISettings();
        ApplyButtonStyles();

        // 4. 부모 초기화 실행 (버튼 기본 상태 저장 및 게임 시작)
        base.Initialize();
    }

    // UI 설정 적용
    private void ApplyUISettings()
    {
        if (currentSetting == null || UIManager.Instance == null) return;

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

        // 상단 이미지 적용
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

    // 버튼 스타일 적용
    private void ApplyButtonStyles()
    {
        if (JsonLoader.Instance != null && UIManager.Instance != null && answerButtons != null)
        {
            Settings globalSettings = JsonLoader.Instance.LoadJsonData<Settings>("Settings.json");
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
                        }
                    }
                }
            }
        }
    }

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
            else Debug.LogWarning($"Level {selectedLevel} Problems Not Found");
        }
    }

    protected override void SetupSpecificQuestionUI(NumberSystemQuestion q)
    {
        // 상태 초기화
        _currentSequenceIndex = 0;
        _foundAnswerCount = 0;
        _foundAnswersSet = new HashSet<string>();

        // 진행도 업데이트
        UpdateProgressImage(q.level, currentQuestionIndex);

        // 1. 좌우 랜덤 배치 결정 (50%)
        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform contentParent = isTextLeft ? rightQuestionZone : leftQuestionZone;

        // 2. 텍스트 설정 및 배치
        if (questionTextObj && textParent)
        {
            questionTextObj.transform.SetParent(textParent, false);
            bool hasText = !string.IsNullOrEmpty(q.questionText);
            
            if (hasText)
            {
                questionTextObj.text = q.questionText;
                questionTextObj.gameObject.SetActive(true);
            }
            else
            {
                questionTextObj.gameObject.SetActive(false);
            }
        }

        // 3. 미디어(비디오/이미지) 설정 및 배치
        bool hasVideo = q.questionVideo != null && !string.IsNullOrEmpty(q.questionVideo.fileName);
        bool hasImage = q.questionImage != null && !string.IsNullOrEmpty(q.questionImage.sourceImage);

        if (hasVideo && questionVideoObject)
        {
            questionVideoObject.transform.SetParent(contentParent, false);
            PlayVideo(q.questionVideo);
        }
        else if (hasImage && questionImageObj)
        {
            questionImageObj.transform.SetParent(contentParent, false);
            UIManager.Instance.SetImageObj(questionImageObj.gameObject, q.questionImage);
            questionImageObj.gameObject.SetActive(true);
        }
    }

    protected override void SetupAnswerButtons(NumberSystemQuestion q)
    {
        List<string> options = new List<string>();
        if (q.correctAnswers != null) options.AddRange(q.correctAnswers);
        if (q.wrongAnswers != null)
        {
            int remaining = 4 - options.Count;
            if (remaining > 0) options.AddRange(q.wrongAnswers.Take(remaining));
        }

        // 셔플
        options = options.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();

        // 1. 배치 (활성화된 버튼만 반으로 나누어 양쪽 영역에 배치)
        List<GameObject> activeButtons = shuffledButtons.Take(options.Count).ToList();
        int halfCount = Mathf.CeilToInt(activeButtons.Count / 2f);

        PlaceButtonsInArea(activeButtons.Take(halfCount).ToList(), leftAreaRect);
        PlaceButtonsInArea(activeButtons.Skip(halfCount).ToList(), rightAreaRect);

        // 2. 버튼 데이터 설정
        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            Button btn = btnObj.GetComponent<Button>();
            Image btnImage = btnObj.GetComponent<Image>();
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            
            btn.interactable = true;
            btn.onClick.RemoveAllListeners();

            // 기본값 복구 (이미지, 색상)
            if (btnImage && defaultButtonSprite)
            {
                btnImage.sprite = defaultButtonSprite;
                btnImage.color = defaultButtonColor;
            }
            
            // 크기 복구 (중요: 이전 문제에서 변경된 크기 리셋)
            if (btnRect && defaultButtonSize != Vector2.zero)
            {
                btnRect.sizeDelta = defaultButtonSize;
            }

            // 그라데이션 복구
            var gradient = btnObj.GetComponent<ImageGlobalGradient>();
            if(gradient) gradient.enabled = true;

            if (i < options.Count)
            {
                string text = options[i];
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                
                // 이미지 매핑 확인 (NumberSystem 전용 로직)
                AnswerImagePair pair = GetAnswerImagePair(q, text);
                
                if (pair != null && !string.IsNullOrEmpty(pair.imagePath))
                {
                    // 텍스트 숨기고 이미지 적용
                    if (tmp) tmp.text = "";
                    Sprite s = LoadSpriteFromStreamingAssets(pair.imagePath);
                    
                    if (s && btnImage)
                    {
                        btnImage.sprite = s;
                        btnImage.color = Color.white;
                        if(gradient) gradient.enabled = false;
                        
                        // [크기 변경] 설정된 사이즈가 있다면 적용
                        if (pair.size != Vector2.zero && btnRect)
                        {
                            btnRect.sizeDelta = pair.size;
                        }
                    }
                }
                else
                {
                    // 일반 텍스트 표시
                    if (tmp) tmp.text = text;
                }

                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
                btnObj.SetActive(true);
            }
            else
            {
                btnObj.SetActive(false);
            }
        }
    }

    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        if (isProcessing) return;

        bool isCorrect = false;
        bool isLevelClear = false;

        switch (currentQuestion.type)
        {
            case QuestionType.SingleChoice:
                if (currentQuestion.correctAnswers.Contains(clickedText)) 
                { 
                    isCorrect = true; 
                    isLevelClear = true; 
                }
                break;

            case QuestionType.MultipleChoice:
                if (currentQuestion.correctAnswers.Contains(clickedText))
                {
                    if (!_foundAnswersSet.Contains(clickedText))
                    {
                        isCorrect = true;
                        _foundAnswersSet.Add(clickedText);
                        _foundAnswerCount++;
                        btnObj.SetActive(false);
                        
                        if (_foundAnswerCount >= currentQuestion.correctAnswers.Length)
                            isLevelClear = true;
                    }
                }
                break;

            case QuestionType.Sequence:
                if (_currentSequenceIndex < currentQuestion.correctAnswers.Length)
                {
                    string target = currentQuestion.correctAnswers[_currentSequenceIndex];
                    if (clickedText == target)
                    {
                        isCorrect = true;
                        _currentSequenceIndex++;
                        btnObj.SetActive(false);

                        if (_currentSequenceIndex >= currentQuestion.correctAnswers.Length)
                            isLevelClear = true;
                    }
                }
                break;
        }

        if (isCorrect)
        {
            if (isLevelClear) HandleCorrectAnswer();
        }
        else
        {
            HandleWrongAnswer();
        }
    }

    private AnswerImagePair GetAnswerImagePair(NumberSystemQuestion q, string text)
    {
        if (q.answerImages == null) return null;
        return q.answerImages.FirstOrDefault(x => x.answerText == text);
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