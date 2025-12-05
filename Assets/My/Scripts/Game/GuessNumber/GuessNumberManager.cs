using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class GuessNumberManager : MonoBehaviour
{
    [Header("Top UI")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI gameTypeText;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Layout Areas")]
    [SerializeField] private RectTransform leftAreaRect;
    [SerializeField] private RectTransform rightAreaRect;
    [SerializeField] private float buttonMargin = 20f; // 버튼 배치 여백 (MathManager 스타일)

    [Header("Question UI")]
    [SerializeField] private TextMeshProUGUI leftQuestionText;
    [SerializeField] private TextMeshProUGUI rightQuestionText;
    [SerializeField] private GameObject leftQuestionZone;
    [SerializeField] private GameObject rightQuestionZone;

    [Header("Buttons")]
    [SerializeField] private GameObject[] answerButtons;
    [SerializeField] private Button backButton;

    private GuessNumberData _data;
    private List<GuessNumberQuestion> _currentLevelQuestions;
    
    private int _currentQuestionIndex = 0;
    private int _totalQuestions = 4;

    // -- 로직 처리를 위한 상태 변수 --
    private GuessNumberQuestion _currentQuestion;
    private List<string> _remainingCorrectAnswers;
    private int _sequenceIndex;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        int selectedLevel = LevelSelectContext.SelectedLevel;
        if (levelText) levelText.text = $"LEVEL {selectedLevel}";
        if (gameTypeText) gameTypeText.text = "수 맞추기";
        
        if (backButton)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => SceneManager.LoadScene("LevelSelect"));
        }

        LoadGameData();

        if (_data != null && _data.questions != null)
        {
            var levelQuestions = _data.questions
                .Where(q => q.level == selectedLevel)
                .ToList();

            if (levelQuestions.Count == 0)
            {
                Debug.LogWarning($"Level {selectedLevel} 데이터가 없습니다.");
                return;
            }

            int count = Mathf.Min(levelQuestions.Count, _totalQuestions);
            _currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
            _totalQuestions = _currentLevelQuestions.Count;
            _currentQuestionIndex = 0;
            
            SetQuestion(_currentQuestionIndex);
        }
    }

    private void LoadGameData()
    {
        if (JsonLoader.Instance != null)
        {
            _data = JsonLoader.Instance.LoadJsonData<GuessNumberData>("JSON/GuessNumber.json");
        }
    }

    private void SetQuestion(int index)
    {
        if (index >= _currentLevelQuestions.Count)
        {
            Debug.Log("Game Clear!");
            SceneManager.LoadScene("LevelSelect");
            return;
        }

        _currentQuestion = _currentLevelQuestions[index];
        
        // 상태 초기화
        _remainingCorrectAnswers = new List<string>(_currentQuestion.correctAnswers);
        _sequenceIndex = 0;

        // UI 표시
        if (progressText) progressText.text = $"{index + 1} / {_totalQuestions}";
        
        bool isLeft = Random.Range(0, 2) == 0;
        if (leftQuestionText) leftQuestionText.gameObject.SetActive(isLeft);
        if (rightQuestionText) rightQuestionText.gameObject.SetActive(!isLeft);
        if (leftQuestionZone) leftQuestionZone.SetActive(isLeft);
        if (rightQuestionZone) rightQuestionZone.SetActive(!isLeft);

        string qText = _currentQuestion.questionText;
        
        if (isLeft && leftQuestionText) leftQuestionText.text = qText;
        if (!isLeft && rightQuestionText) rightQuestionText.text = qText;

        SetupAndPlaceButtons(_currentQuestion);
    }

    private void SetupAndPlaceButtons(GuessNumberQuestion q)
    {
        int totalSlots = 4; // 버튼 슬롯 수

        // 1. 화면에 표시할 텍스트 리스트 구성
        List<string> displayTexts = new List<string>();

        if (q.type == QuestionType.Sequence)
        {
            displayTexts.AddRange(q.correctAnswers.Take(totalSlots));
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
            {
                var wrongs = q.wrongAnswers.OrderBy(x => Random.value).Take(slotsLeft);
                displayTexts.AddRange(wrongs);
            }
        }
        else // SingleChoice
        {
            displayTexts.Add(q.correctAnswers[0]);
            if (q.wrongAnswers != null)
            {
                var wrongs = q.wrongAnswers.OrderBy(x => Random.value).Take(totalSlots - 1);
                displayTexts.AddRange(wrongs);
            }
        }

        // 2. 텍스트 셔플
        List<string> shuffledTexts = displayTexts.OrderBy(x => Random.value).ToList();

        // 3. 버튼 오브젝트 배치 (MathManager 스타일 적용)
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();
        
        // 왼쪽 영역에 2개, 오른쪽 영역에 2개 할당
        List<GameObject> leftButtons = new List<GameObject> { shuffledButtons[0], shuffledButtons[1] };
        List<GameObject> rightButtons = new List<GameObject> { shuffledButtons[2], shuffledButtons[3] };

        // MathManager의 PlaceButtonsInArea 로직 사용
        PlaceButtonsInArea(leftButtons, leftAreaRect);
        PlaceButtonsInArea(rightButtons, rightAreaRect);

        // 4. 데이터 주입
        for (int i = 0; i < totalSlots; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            string text = (i < shuffledTexts.Count) ? shuffledTexts[i] : "";
            
            TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = text;

            Button btn = btnObj.GetComponent<Button>();
            
            btn.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(text))
            {
                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
            }
            
            btnObj.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    // 답안 클릭 처리
    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        bool isCorrectAction = false;
        bool isLevelClear = false;

        switch (_currentQuestion.type)
        {
            case QuestionType.SingleChoice:
                if (clickedText == _currentQuestion.correctAnswers[0])
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
                if (_sequenceIndex < _currentQuestion.correctAnswers.Length && 
                    clickedText == _currentQuestion.correctAnswers[_sequenceIndex])
                {
                    isCorrectAction = true;
                    _sequenceIndex++;
                    btnObj.SetActive(false);
                    if (_sequenceIndex >= _currentQuestion.correctAnswers.Length) isLevelClear = true;
                }
                break;
        }

        if (isCorrectAction)
        {
            Debug.Log("Good!");
            if (isLevelClear)
            {
                Debug.Log("Level Clear!");
                _currentQuestionIndex++;
                SetQuestion(_currentQuestionIndex);
            }
        }
        else
        {
            Debug.Log("Wrong!");
        }
    }

    /// <summary>
    /// MathManager의 배치 로직을 이식함 (Grid + Jitter)
    /// 주어진 영역을 격자로 나누고 랜덤한 오프셋을 주어 배치
    /// </summary>
    private void PlaceButtonsInArea(List<GameObject> buttonsToPlace, RectTransform areaRect)
    {
        if (areaRect == null || buttonsToPlace == null || buttonsToPlace.Count == 0)
            return;

        // 기준이 될 버튼 크기 계산 (첫 번째 버튼 기준)
        GameObject sampleObj = buttonsToPlace[0];
        RectTransform sampleRt = sampleObj.GetComponent<RectTransform>();
        if (sampleRt == null) return;

        Rect rect = areaRect.rect;
        Vector2 halfAreaSize = rect.size * 0.5f;

        // MathManager와 동일한 2행 3열 격자 설정 (영역이 좁다면 2x1, 2x2 등으로 조절 가능)
        // 여기서는 버튼이 2개뿐이므로 넉넉하게 2x2 혹은 1x2 등을 써도 되지만, 
        // MathManager와의 통일성을 위해 유사한 로직 사용
        const int columns = 1;  // 좌우 영역이 좁으므로 1열로 설정 (위/아래 배치 유도) 또는 넓으면 2열
        const int rows = 2;     // 2행

        // *참고: 좌/우 패널이 세로로 긴 형태라면 1열 2행이 적절하고, 
        // 넓은 형태라면 2열 1행이나 MathManager처럼 3열 2행을 써도 됩니다.
        // 여기서는 "겹치지 않는 배치"를 위해 2행 1열(총 2슬롯)로 단순화하여 위/아래 랜덤 배치를 구현합니다.
        
        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;

        Vector3 scale = sampleRt.localScale;
        float buttonWidth = sampleRt.sizeDelta.x * scale.x;
        float buttonHeight = sampleRt.sizeDelta.y * scale.y;

        // 최대 흔들림(Jitter) 범위 계산
        float maxJitterX = Mathf.Max(0f, (cellWidth - buttonWidth) * 0.5f - buttonMargin);
        float maxJitterY = Mathf.Max(0f, (cellHeight - buttonHeight) * 0.5f - buttonMargin);

        // 슬롯 좌표 생성
        List<Vector2> slots = new List<Vector2>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                float x = -halfAreaSize.x + cellWidth * (col + 0.5f);
                float y =  halfAreaSize.y - cellHeight * (row + 0.5f);
                slots.Add(new Vector2(x, y));
            }
        }

        // 슬롯 셔플
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        // 버튼 배치 적용
        int count = Mathf.Min(buttonsToPlace.Count, slots.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = buttonsToPlace[i];
            RectTransform rt = obj.GetComponent<RectTransform>();
            
            Vector2 basePos = slots[i];

            // 랜덤 오프셋 적용
            float offsetX = maxJitterX > 0f ? Random.Range(-maxJitterX, maxJitterX) : 0f;
            float offsetY = maxJitterY > 0f ? Random.Range(-maxJitterY, maxJitterY) : 0f;

            rt.SetParent(areaRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = basePos + new Vector2(offsetX, offsetY);
        }
    }
}