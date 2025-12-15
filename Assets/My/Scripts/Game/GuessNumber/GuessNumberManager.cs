using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using Random = UnityEngine.Random;

public class GuessNumberManager : BaseGameManager<GuessNumberSetting, GuessNumberQuestion>
{
    [Header("--- GuessNumber Specific ---")]
    [SerializeField] private Transform leftQuestionZone;
    [SerializeField] private Transform rightQuestionZone;

    // 진행 상태 변수
    private int _sequenceIndex = 0;
    private List<string> _remainingCorrectAnswers;

    protected override string GetJsonFileName() => "GuessNumber.json";

    protected override int GetQuestionLevel(GuessNumberQuestion question) => question.level;

    // [중요] 초기화 순서 수정
    protected override void Initialize()
    {
        // 1. 데이터 먼저 로드
        LoadGameData();

        // 2. 스타일 및 UI 세팅 적용 (부모 Initialize보다 먼저 실행해야 함)
        ApplyUISettings();
        ApplyButtonStyles();

        // 3. 부모 초기화 실행 (버튼 기본 상태 저장 및 게임 시작)
        base.Initialize();
    }

    // UI 세팅 적용 (이미지, 버튼 등)
    private void ApplyUISettings()
    {
        if (currentSetting == null || UIManager.Instance == null) return;

        this.buttonMargin = currentSetting.buttonMargin;

        // 공통 UI 요소 설정
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

    // 버튼 스타일 적용 (Global Settings)
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
                            tmp.enableWordWrapping = true; // GuessNumber는 텍스트가 길 수 있으므로 래핑 활성화
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
            else
            {
                Debug.LogWarning($"Level {selectedLevel} 데이터가 없습니다.");
            }
        }
    }

    // 개별 문제 UI 세팅
    protected override void SetupSpecificQuestionUI(GuessNumberQuestion q)
    {
        // 상태 초기화
        _sequenceIndex = 0;
        _remainingCorrectAnswers = new List<string>(q.correctAnswers); 

        // 진행도 업데이트
        UpdateProgressImage(q.level, currentQuestionIndex);

        // 1. 좌우 랜덤 배치 결정 (50%)
        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform imageParent = isTextLeft ? rightQuestionZone : leftQuestionZone;

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
            // 부모 오브젝트 활성/비활성 처리
            if (textParent.gameObject != null) textParent.gameObject.SetActive(hasText);
        }

        // 3. 이미지 설정 및 배치
        if (questionImageObj && imageParent)
        {
            questionImageObj.transform.SetParent(imageParent, false);
            bool hasImage = q.questionImage != null && !string.IsNullOrEmpty(q.questionImage.sourceImage);

            if (hasImage && UIManager.Instance != null)
            {
                UIManager.Instance.SetImageObj(questionImageObj.gameObject, q.questionImage);
                questionImageObj.gameObject.SetActive(true);
                if (imageParent.gameObject != null) imageParent.gameObject.SetActive(true);
            }
            else
            {
                questionImageObj.gameObject.SetActive(false);
                // 이미지가 없으면 부모 영역도 꺼서 레이아웃 정리 (선택사항)
                if (imageParent.gameObject != null) imageParent.gameObject.SetActive(false);
            }
        }
    }

    // 정답 버튼 세팅
    protected override void SetupAnswerButtons(GuessNumberQuestion q)
    {
        int totalSlots = 4;
        List<string> displayTexts = new List<string>();

        // 문제 유형별 보기 구성
        if (q.type == QuestionType.Sequence)
        {
            displayTexts.AddRange(q.correctAnswers.Take(totalSlots));
            int slotsLeft = totalSlots - displayTexts.Count;
            if (slotsLeft > 0 && q.wrongAnswers != null)
            {
                displayTexts.AddRange(q.wrongAnswers.OrderBy(x => Random.value).Take(slotsLeft));
            }
        }
        else if (q.type == QuestionType.MultipleChoice)
        {
            displayTexts.AddRange(q.correctAnswers);
            if (displayTexts.Count > totalSlots)
            {
                displayTexts = displayTexts.Take(totalSlots).ToList();
                _remainingCorrectAnswers = new List<string>(displayTexts);
            }
            
            int slotsLeft = totalSlots - displayTexts.Count;
            if (slotsLeft > 0 && q.wrongAnswers != null)
                displayTexts.AddRange(q.wrongAnswers.OrderBy(x => Random.value).Take(slotsLeft));
        }
        else // SingleChoice
        {
            if(q.correctAnswers.Length > 0) displayTexts.Add(q.correctAnswers[0]);
            if (q.wrongAnswers != null)
                displayTexts.AddRange(q.wrongAnswers.OrderBy(x => Random.value).Take(totalSlots - 1));
        }

        // 셔플
        List<string> shuffledTexts = displayTexts.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();
        
        // 버튼 배치 (2개씩 분할)
        List<GameObject> leftButtons = new List<GameObject> { shuffledButtons[0], shuffledButtons[1] };
        List<GameObject> rightButtons = new List<GameObject> { shuffledButtons[2], shuffledButtons[3] };

        PlaceButtonsInArea(leftButtons, leftAreaRect);
        PlaceButtonsInArea(rightButtons, rightAreaRect);

        // 버튼 데이터 설정
        for (int i = 0; i < totalSlots; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            string text = (i < shuffledTexts.Count) ? shuffledTexts[i] : "";
            
            // 텍스트 설정
            TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = text;

            // 버튼 리셋 (스타일 적용된 기본값으로 복구)
            Button btn = btnObj.GetComponent<Button>();
            Image btnImage = btnObj.GetComponent<Image>();
            
            if (btnImage && defaultButtonSprite)
            {
                btnImage.sprite = defaultButtonSprite;
                btnImage.color = defaultButtonColor;
            }
            
            // 그라데이션 켜기
            var gradient = btnObj.GetComponent<ImageGlobalGradient>();
            if (gradient) gradient.enabled = true;

            // 리스너 연결
            btn.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(text))
            {
                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
            }
            
            btnObj.SetActive(!string.IsNullOrEmpty(text));
            btn.interactable = true;
        }
    }

    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        if (isProcessing) return;

        bool isCorrectAction = false;
        bool isLevelClear = false;

        switch (currentQuestion.type)
        {
            case QuestionType.SingleChoice:
                if (currentQuestion.correctAnswers.Length > 0 && clickedText == currentQuestion.correctAnswers[0]) 
                { 
                    isCorrectAction = true; 
                    isLevelClear = true; 
                }
                break;

            case QuestionType.MultipleChoice:
                if (_remainingCorrectAnswers.Contains(clickedText))
                {
                    isCorrectAction = true;
                    _remainingCorrectAnswers.Remove(clickedText);
                    btnObj.SetActive(false); // 맞춘 버튼 숨기기
                    if (_remainingCorrectAnswers.Count == 0) isLevelClear = true;
                }
                break;

            case QuestionType.Sequence:
                if (_sequenceIndex < currentQuestion.correctAnswers.Length && 
                    clickedText == currentQuestion.correctAnswers[_sequenceIndex])
                {
                    isCorrectAction = true;
                    _sequenceIndex++;
                    btnObj.SetActive(false); // 맞춘 순서 숨기기
                    if (_sequenceIndex >= currentQuestion.correctAnswers.Length) isLevelClear = true;
                }
                break;
        }

        if (isCorrectAction)
        {
            Debug.Log("Correct!");
            if (isLevelClear)
            {
                HandleCorrectAnswer();
            }
        }
        else
        {
            Debug.Log("Wrong!");
            HandleWrongAnswer();
        }
    }

    private void UpdateProgressImage(int level, int index)
    {
        if (!progressImage || currentSetting?.levelProgresses == null) return;
        int lvIdx = level - 1;
        if (lvIdx >= 0 && lvIdx < currentSetting.levelProgresses.Length)
        {
            var steps = currentSetting.levelProgresses[lvIdx].steps;
            if (index >= 0 && index < steps.Length)
            {
                UIManager.Instance.SetImageObj(progressImage.gameObject, steps[index]);
                progressImage.gameObject.SetActive(true);
            }
        }
    }
}