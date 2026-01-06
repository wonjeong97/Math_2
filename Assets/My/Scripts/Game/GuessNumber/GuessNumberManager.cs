using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// '수 맞추기(GuessNumber)' 게임의 진행 관리 매니저.
/// 문제 출제, 정답/오답 판별, 진행도 관리 등을 담당.
/// </summary>
public class GuessNumberManager : BaseGameManager<GuessNumberSetting, GuessNumberQuestion>
{
    [Header("--- GuessNumber Specific ---")] 
    [SerializeField] private GameObject backgroundObj;
    [SerializeField] private Transform leftQuestionZone; // 왼쪽 문제 배치 구역
    [SerializeField] private Transform rightQuestionZone; // 오른쪽 문제 배치 구역

    private int _sequenceIndex; // 순서대로 누르기(Sequence) 문제용 인덱스
    private List<string> _remainingCorrectAnswers; // 다중 선택(MultipleChoice) 문제용 남은 정답 목록
    
    
    // JSON 파일명 정의
    protected override string GetJsonFileName() => "GuessNumber.json";

    // 문제 레벨 반환
    protected override int GetQuestionLevel(GuessNumberQuestion question) => question.level;

    // 버튼 텍스트 자동 줄바꿈 활성화
    protected override bool EnableButtonWordWrapping => false;

    protected override void OnSetupChildComponents()
    {
        // 배경 이미지 설정
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
    /// 게임 시작 로직.
    /// 선택된 레벨의 문제를 필터링하고 첫 번째 문제를 출제.
    /// </summary>
    protected override void StartGameLogic()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("GuessNumber_BGM");
        }

        int selectedLevel = LevelSelectContext.SelectedLevel > 0 ? LevelSelectContext.SelectedLevel : 1;

        // BaseManager의 managerSetting 사용
        if (managerSetting?.questions != null)
        {
            var levelQuestions = managerSetting.questions.Where(q => q.level == selectedLevel).ToList();
            if (levelQuestions.Count > 0)
            {
                int count = Mathf.Min(levelQuestions.Count, totalQuestions);
                // 랜덤 셔플 후 문제 추출
                currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
                totalQuestions = currentLevelQuestions.Count;
                SetQuestionBase(0);
            }
            else Debug.LogWarning($"Level {selectedLevel} 데이터가 없습니다.");
        }
    }

    /// <summary> 개별 문제 UI 설정 (텍스트, 이미지 배치 및 초기화). </summary>
    protected override void SetupSpecificQuestionUI(GuessNumberQuestion q)
    {
        // 상태 초기화
        _sequenceIndex = 0;
        _remainingCorrectAnswers = new List<string>(q.correctAnswers);

        // 진행도 업데이트
        UpdateProgressImage(q.level, currentQuestionIndex);

        // 좌우 랜덤 배치 결정
        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform imageParent = isTextLeft ? rightQuestionZone : leftQuestionZone;

        // 텍스트 설정
        if (questionTextObj && textParent)
        {
            questionTextObj.transform.SetParent(textParent, false);
            bool hasText = !string.IsNullOrEmpty(q.questionText);
            questionTextObj.text = hasText ? q.questionText : "";
            questionTextObj.gameObject.SetActive(hasText);
            // 부모 오브젝트 활성/비활성 처리 (레이아웃 정리)
            if (textParent.gameObject != null) textParent.gameObject.SetActive(hasText);
        }

        // 이미지 설정
        if (questionImageObj && imageParent)
        {
            questionImageObj.transform.SetParent(imageParent, false);
            bool hasImage = q.questionImage != null && !string.IsNullOrEmpty(q.questionImage.sourceImage);

            if (hasImage && UIManager.Instance != null)
            {
                UIManager.Instance.SetImageObj(questionImageObj.gameObject, q.questionImage,
                        this.GetCancellationTokenOnDestroy())
                    .Forget(ex => Debug.LogError($"[GuessNumberManager] 문제 이미지 설정 실패: {ex.Message}"));
                questionImageObj.gameObject.SetActive(true);
                if (imageParent.gameObject != null) imageParent.gameObject.SetActive(true);
            }
            else
            {
                questionImageObj.gameObject.SetActive(false);
                if (imageParent.gameObject != null) imageParent.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 정답 버튼 설정 및 배치.
    /// 문제 유형(Sequence, MultipleChoice 등)에 따라 보기 구성.
    /// </summary>
    protected override void SetupAnswerButtons(GuessNumberQuestion q)
    {
        int totalSlots = 4;
        List<string> displayTexts = new List<string>();

        // 1. 문제 유형별 보기 텍스트 구성
        if (q.type == QuestionType.Sequence)
        {
            // 순서 문제는 정답 순서대로 배치하거나 섞음
            displayTexts.AddRange(q.correctAnswers.Take(totalSlots));
            int slotsLeft = totalSlots - displayTexts.Count;
            if (slotsLeft > 0 && q.wrongAnswers != null)
                displayTexts.AddRange(q.wrongAnswers.OrderBy(x => Random.value).Take(slotsLeft));
        }
        else if (q.type == QuestionType.MultipleChoice)
        {
            displayTexts.AddRange(q.correctAnswers);
            // 정답이 슬롯보다 많으면 자름
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
            if (q.correctAnswers.Length > 0) displayTexts.Add(q.correctAnswers[0]);
            if (q.wrongAnswers != null)
                displayTexts.AddRange(q.wrongAnswers.OrderBy(x => Random.value).Take(totalSlots - 1));
        }

        // 2. 텍스트 및 버튼 셔플
        List<string> shuffledTexts = displayTexts.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();

        // 3. 버튼 배치 (좌우 균등 분할) - BaseGameManager 활용
        List<GameObject> leftButtons = new List<GameObject> { shuffledButtons[0], shuffledButtons[1] };
        List<GameObject> rightButtons = new List<GameObject> { shuffledButtons[2], shuffledButtons[3] };

        PlaceButtonsInArea(leftButtons, leftAreaRect);
        PlaceButtonsInArea(rightButtons, rightAreaRect);

        // 4. 버튼 데이터 매핑
        for (int i = 0; i < totalSlots; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            string text = (i < shuffledTexts.Count) ? shuffledTexts[i] : "";

            TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = text;

            Button btn = btnObj.GetComponent<Button>();
            Image btnImage = btnObj.GetComponent<Image>();

            // 기본 스타일로 복구
            RestoreButtonDefault(btn, btnImage);

            // 이벤트 연결
            btn.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(text))
            {
                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
            }

            btnObj.SetActive(!string.IsNullOrEmpty(text));
            btn.interactable = true;
        }
    }

    /// <summary> 정답 버튼 클릭 핸들러. </summary>
    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        if (isProcessing) return;
        bool isCorrectAction = false;
        bool isLevelClear = false;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("Button");
        }

        switch (currentQuestion.type)
        {
            case QuestionType.SingleChoice:
                // 단일 선택: 정답과 일치하면 클리어
                if (currentQuestion.correctAnswers.Length > 0 && clickedText == currentQuestion.correctAnswers[0])
                {
                    isCorrectAction = true;
                    isLevelClear = true;
                }

                break;

            case QuestionType.MultipleChoice:
                // 다중 선택: 정답 목록에 있으면 제거, 다 찾으면 클리어
                if (_remainingCorrectAnswers.Contains(clickedText))
                {
                    isCorrectAction = true;
                    _remainingCorrectAnswers.Remove(clickedText);
                    btnObj.SetActive(false); // 맞춘 버튼 숨기기
                    if (_remainingCorrectAnswers.Count == 0) isLevelClear = true;
                }

                break;

            case QuestionType.Sequence:
                // 순서 문제: 현재 순서와 일치하면 진행, 끝까지 가면 클리어
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
            if (isLevelClear)
            {
                HandleCorrectAnswer(); // 정답 처리
            }
        }
        else
        {
            HandleWrongAnswer(); // 오답 처리
        }
    }
}