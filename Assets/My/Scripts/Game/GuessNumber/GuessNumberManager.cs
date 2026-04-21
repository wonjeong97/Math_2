using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// '수 맞추기(GuessNumber)' 게임의 진행을 관리함.
/// 문제 출제, 정답 판별, 진행도 관리 등을 담당.
/// </summary>
public class GuessNumberManager : BaseGameManager<GuessNumberSetting, GuessNumberQuestion>
{
    [Header("--- GuessNumber Specific ---")] 
    [SerializeField] private GameObject backgroundObj;
    [SerializeField] private Transform leftQuestionZone; 
    [SerializeField] private Transform rightQuestionZone; 

    private int _sequenceIndex; 
    private List<string> _remainingCorrectAnswers; 
    
    protected override string JsonPath => GameConstants.Path.JsonGuessNumber;
    protected override int GetQuestionLevel(GuessNumberQuestion question) => question.level;
    protected override bool EnableButtonWordWrapping => false;

    protected override void OnSetupChildComponents()
    {
        if (backgroundObj != null && managerSetting != null && managerSetting.backgroundImage != null && UIManager.Instance != null)
        {
            UIManager.Instance.SetImageObj(backgroundObj, managerSetting.backgroundImage, this.GetCancellationTokenOnDestroy()).Forget();
        }
        else if (UIManager.Instance == null)
        {
            Debug.LogError("[GuessNumberManager] UIManager.Instance is null");
        }
    }

    /// <summary>
    /// 게임 로직을 시작함.
    /// 선택된 레벨의 문제를 필터링하고 첫 번째 문제를 출제.
    /// </summary>
    protected override void StartGameLogic()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(GameConstants.Sound.GuessNumberBGM);
        }

        int selectedLevel = LevelSelectContext.SelectedLevel > 0 ? LevelSelectContext.SelectedLevel : 1;

        if (managerSetting?.questions != null)
        {
            var levelQuestions = managerSetting.questions.Where(q => q.level == selectedLevel).ToList();
            if (levelQuestions.Count > 0)
            {
                int count = Mathf.Min(levelQuestions.Count, totalQuestions);
                currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
                totalQuestions = currentLevelQuestions.Count;
                SetQuestionBase(0);
            }
            else Debug.LogWarning($"Level {selectedLevel} 데이터가 없습니다.");
        }
    }

    /// <summary> 개별 문제 UI를 설정함 (텍스트, 이미지 배치 및 초기화). </summary>
    protected override void SetupSpecificQuestionUI(GuessNumberQuestion q)
    {
        _sequenceIndex = 0;
        _remainingCorrectAnswers = new List<string>(q.correctAnswers);

        UpdateProgressImage(q.level, currentQuestionIndex);

        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform imageParent = isTextLeft ? rightQuestionZone : leftQuestionZone;

        // 1. 텍스트 설정
        if (questionTextObj && textParent)
        {
            questionTextObj.transform.SetParent(textParent, false);
            bool hasText = !string.IsNullOrEmpty(q.questionText);
            questionTextObj.text = hasText ? q.questionText : "";
            
            // 텍스트 내용이 없어도 Zone은 항상 켜둠
            questionTextObj.gameObject.SetActive(hasText); 
            if (textParent.gameObject != null) textParent.gameObject.SetActive(true); 
        }

        // 2. 이미지 설정
        if (questionImageObj && imageParent)
        {
            questionImageObj.transform.SetParent(imageParent, false);
            bool hasImage = q.questionImage != null && !string.IsNullOrEmpty(q.questionImage.sourceImage);

            if (hasImage && UIManager.Instance != null)
            {
                UIManager.Instance.SetImageObj(questionImageObj.gameObject, q.questionImage, this.GetCancellationTokenOnDestroy())
                    .Forget(ex => Debug.LogError($"[GuessNumberManager] 문제 이미지 설정 실패: {ex.Message}"));
                questionImageObj.gameObject.SetActive(true);
            }
            else
            {
                questionImageObj.gameObject.SetActive(false);
            }
            
            // 이미지가 없어도 Zone은 항상 켜둠
            if (imageParent.gameObject != null) imageParent.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// 정답 버튼을 설정하고 배치함.
    /// 문제 유형에 따라 보기를 구성.
    /// </summary>
    protected override void SetupAnswerButtons(GuessNumberQuestion q)
    {
        int totalSlots = 4;
        List<string> displayTexts = new List<string>();

        // 1. 문제 유형별 보기 텍스트 구성
        if (q.type == QuestionType.Sequence)
        {
            displayTexts.AddRange(q.correctAnswers.Take(totalSlots));
            int slotsLeft = totalSlots - displayTexts.Count;
            if (slotsLeft > 0 && q.wrongAnswers != null)
                displayTexts.AddRange(q.wrongAnswers.OrderBy(x => Random.value).Take(slotsLeft));
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
        else 
        {
            if (q.correctAnswers.Length > 0) displayTexts.Add(q.correctAnswers[0]);
            if (q.wrongAnswers != null)
                displayTexts.AddRange(q.wrongAnswers.OrderBy(x => Random.value).Take(totalSlots - 1));
        }

        // 2. 텍스트 및 버튼 셔플
        List<string> shuffledTexts = displayTexts.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();

        // 3. 버튼 배치
        List<GameObject> leftButtons = new List<GameObject> { shuffledButtons[0], shuffledButtons[1] };
        List<GameObject> rightButtons = new List<GameObject> { shuffledButtons[2], shuffledButtons[3] };

        UILayoutUtility.PlaceObjectsRandomlyInGrid(leftButtons, leftAreaRect, managerSetting.buttonMargin);
        UILayoutUtility.PlaceObjectsRandomlyInGrid(rightButtons, rightAreaRect, managerSetting.buttonMargin);

        // 4. 버튼 데이터 매핑
        for (int i = 0; i < totalSlots; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            string text = (i < shuffledTexts.Count) ? shuffledTexts[i] : "";

            TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = text;

            Button btn = btnObj.GetComponent<Button>();
            Image btnImage = btnObj.GetComponent<Image>();

            RestoreButtonDefault(btn, btnImage);

            btn.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(text))
            {
                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
            }

            btnObj.SetActive(!string.IsNullOrEmpty(text));
            btn.interactable = true;
        }
    }

    /// <summary> 정답 버튼 클릭을 처리함. </summary>
    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {   
        if (GameManager.Instance) GameManager.Instance.ResetInactivityTimer();
        if (isProcessing) return;
        bool isCorrectAction = false;
        bool isLevelClear = false;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(GameConstants.Sound.ButtonClick);
        }

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
                    btnObj.SetActive(false); 
                    if (_remainingCorrectAnswers.Count == 0) isLevelClear = true;
                }
                break;

            case QuestionType.Sequence:
                if (_sequenceIndex < currentQuestion.correctAnswers.Length &&
                    clickedText == currentQuestion.correctAnswers[_sequenceIndex])
                {
                    isCorrectAction = true;
                    _sequenceIndex++;
                    btnObj.SetActive(false);
                    if (_sequenceIndex >= currentQuestion.correctAnswers.Length) isLevelClear = true;
                }
                break;
        }

        if (isCorrectAction)
        {
            if (isLevelClear) HandleCorrectAnswer(); 
        }
        else
        {
            HandleWrongAnswer(); 
        }
    }
}