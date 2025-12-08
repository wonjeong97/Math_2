using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

#region Data Structures

// 단계별 이미지 설정을 위한 래퍼 클래스
[Serializable]
public class LevelProgressSetting
{
    public ImageSetting[] steps; // 4단계 (1/4, 2/4, 3/4, 4/4)
}

[Serializable]
public class GuessNumberSetting
{
    // [퀴즈 데이터]
    public GuessNumberQuestion[] questions;
    
    // [UI 설정 데이터]
    public ImageSetting[] levelImages;       // 레벨별 상단 타이틀
    public ImageSetting[] gameTypeImages;    // 레벨별 게임 타입 아이콘
    public LevelProgressSetting[] levelProgresses; 
    public ButtonSetting backButton;         // 뒤로가기 버튼
    public float buttonMargin = 20f;         // 버튼 간격
}

#endregion

public class GuessNumberManager : MonoBehaviour
{
    [Header("Top UI")]
    [SerializeField] private Image levelImage;
    [SerializeField] private Image gameTypeImage;
    [SerializeField] private Image progressImage; // 진행바 이미지 (계속 갱신됨)

    [Header("Layout Areas")]
    [SerializeField] private RectTransform leftAreaRect;
    [SerializeField] private RectTransform rightAreaRect;
    
    private float buttonMargin = 20f; 

    [Header("Question UI")]
    [SerializeField] private TextMeshProUGUI leftQuestionText;
    [SerializeField] private TextMeshProUGUI rightQuestionText;
    [SerializeField] private GameObject leftQuestionZone;
    [SerializeField] private GameObject rightQuestionZone;

    [Header("Buttons")]
    [SerializeField] private GameObject[] answerButtons;
    [SerializeField] private Button backButton;

    private GuessNumberSetting setting;
    
    private List<GuessNumberQuestion> _currentLevelQuestions;
    private int _currentQuestionIndex = 0;
    private int _totalQuestions = 4; // 레벨당 4문제

    private GuessNumberQuestion _currentQuestion;
    private List<string> _remainingCorrectAnswers;
    private int _sequenceIndex;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // 1. 데이터 로드
        LoadGameData();

        if (setting == null)
        {
            Debug.LogError("[GuessNumberManager] Data Load Failed.");
            return;
        }

        // 2. 기본 UI 설정 (뒤로가기 버튼 등)
        ApplyUISettings();

        // 3. 현재 레벨 확인 및 이미지 적용
        int selectedLevel = LevelSelectContext.SelectedLevel;
        if (selectedLevel <= 0) selectedLevel = 1;
        
        ApplyLevelImages(selectedLevel);

        // 4. 뒤로가기 버튼 이벤트
        if (backButton)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => SceneManager.LoadScene("LevelSelect"));
        }

        // 5. 퀴즈 데이터 세팅
        if (setting.questions != null)
        {
            var levelQuestions = setting.questions
                .Where(q => q.level == selectedLevel)
                .ToList();

            if (levelQuestions.Count == 0)
            {
                Debug.LogWarning($"Level {selectedLevel} 데이터가 없습니다.");
                return;
            }

            // 문제 랜덤 섞기 및 개수 제한 (4개)
            int count = Mathf.Min(levelQuestions.Count, _totalQuestions);
            _currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
            _totalQuestions = _currentLevelQuestions.Count;
            _currentQuestionIndex = 0;
            
            // 첫 문제 세팅 (여기서 1/4 진행바도 설정됨)
            SetQuestion(_currentQuestionIndex);
        }
    }

    private void LoadGameData()
    {
        if (JsonLoader.Instance != null)
        {
            setting = JsonLoader.Instance.LoadJsonData<GuessNumberSetting>("JSON/GuessNumber.json");
        }
    }

    private void ApplyUISettings()
    {
        if (setting == null || UIManager.Instance == null) return;

        this.buttonMargin = setting.buttonMargin;

        // Back Button
        if (backButton && setting.backButton != null)
            UIManager.Instance.SetButtonObj(backButton.gameObject, setting.backButton).Forget();
            
        // Progress Image는 SetQuestion에서 동적으로 설정하므로 여기서는 제거
    }

    private void ApplyLevelImages(int level)
    {
        if (setting == null || UIManager.Instance == null) return;

        int index = level - 1;

        // 레벨 타이틀
        if (levelImage != null && setting.levelImages != null && index < setting.levelImages.Length)
        {
            var imgData = setting.levelImages[index];
            if (imgData != null)
            {
                UIManager.Instance.SetImageObj(levelImage.gameObject, imgData);
                levelImage.gameObject.SetActive(true);
            }
        }

        // 게임 타입 아이콘
        if (gameTypeImage != null && setting.gameTypeImages != null && index < setting.gameTypeImages.Length)
        {
            var typeData = setting.gameTypeImages[index];
            if (typeData != null)
            {
                UIManager.Instance.SetImageObj(gameTypeImage.gameObject, typeData);
                gameTypeImage.gameObject.SetActive(true);
            }
        }
    }

    // [핵심] 진행바 업데이트 로직
    private void UpdateProgressImage(int level, int questionIndex)
    {
        if (progressImage == null || setting == null || setting.levelProgresses == null) return;

        int levelIdx = level - 1;
        if (levelIdx < 0 || levelIdx >= setting.levelProgresses.Length) return;

        // 해당 레벨의 단계별 이미지 배열 가져오기
        var stepSettings = setting.levelProgresses[levelIdx].steps;
        if (stepSettings == null) return;

        // 인덱스 안전 체크 (0~3)
        if (questionIndex >= 0 && questionIndex < stepSettings.Length)
        {
            var stepData = stepSettings[questionIndex];
            if (stepData != null)
            {
                // UIManager를 통해 이미지 교체
                UIManager.Instance.SetImageObj(progressImage.gameObject, stepData);
                progressImage.gameObject.SetActive(true);
            }
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
        
        // [추가] 문제를 세팅할 때 진행바도 같이 업데이트 (레벨, 현재 인덱스)
        UpdateProgressImage(LevelSelectContext.SelectedLevel, index);

        _currentQuestion = _currentLevelQuestions[index];
        _remainingCorrectAnswers = new List<string>(_currentQuestion.correctAnswers);
        _sequenceIndex = 0;

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
    
    // ... (SetupAndPlaceButtons, OnAnswerClicked, PlaceButtonsInArea 등 나머지 코드는 기존과 동일) ...
    private void SetupAndPlaceButtons(GuessNumberQuestion q)
    {
        int totalSlots = 4;
        List<string> displayTexts = new List<string>();

        if (q.type == QuestionType.Sequence) displayTexts.AddRange(q.correctAnswers.Take(totalSlots));
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
            displayTexts.Add(q.correctAnswers[0]);
            if (q.wrongAnswers != null)
                displayTexts.AddRange(q.wrongAnswers.OrderBy(x => Random.value).Take(totalSlots - 1));
        }

        List<string> shuffledTexts = displayTexts.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();
        
        List<GameObject> leftButtons = new List<GameObject> { shuffledButtons[0], shuffledButtons[1] };
        List<GameObject> rightButtons = new List<GameObject> { shuffledButtons[2], shuffledButtons[3] };

        PlaceButtonsInArea(leftButtons, leftAreaRect);
        PlaceButtonsInArea(rightButtons, rightAreaRect);

        for (int i = 0; i < totalSlots; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            string text = (i < shuffledTexts.Count) ? shuffledTexts[i] : "";
            
            TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp) tmp.text = text;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(text))
                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
            
            btnObj.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        bool isCorrectAction = false;
        bool isLevelClear = false;

        switch (_currentQuestion.type)
        {
            case QuestionType.SingleChoice:
                if (clickedText == _currentQuestion.correctAnswers[0]) { isCorrectAction = true; isLevelClear = true; }
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
                if (_sequenceIndex < _currentQuestion.correctAnswers.Length && clickedText == _currentQuestion.correctAnswers[_sequenceIndex])
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
            Debug.Log("Correct!");
            if (isLevelClear)
            {
                Debug.Log("Level Clear!");
                _currentQuestionIndex++;
                SetQuestion(_currentQuestionIndex);
            }
        }
        else Debug.Log("Wrong!");
    }

    private void PlaceButtonsInArea(List<GameObject> buttonsToPlace, RectTransform areaRect)
    {
        if (areaRect == null || buttonsToPlace == null || buttonsToPlace.Count == 0) return;

        GameObject sampleObj = buttonsToPlace[0];
        RectTransform sampleRt = sampleObj.GetComponent<RectTransform>();
        if (sampleRt == null) return;

        Rect rect = areaRect.rect;
        Vector2 halfAreaSize = rect.size * 0.5f;
        const int columns = 1;
        const int rows = 2;
        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;
        Vector3 scale = sampleRt.localScale;
        float buttonWidth = sampleRt.sizeDelta.x * scale.x;
        float buttonHeight = sampleRt.sizeDelta.y * scale.y;
        float maxJitterX = Mathf.Max(0f, (cellWidth - buttonWidth) * 0.5f - buttonMargin);
        float maxJitterY = Mathf.Max(0f, (cellHeight - buttonHeight) * 0.5f - buttonMargin);

        List<Vector2> slots = new List<Vector2>();
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                float x = -halfAreaSize.x + cellWidth * (col + 0.5f);
                float y =  halfAreaSize.y - cellHeight * (row + 0.5f);
                slots.Add(new Vector2(x, y));
            }

        for (int i = slots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (slots[i], slots[j]) = (slots[j], slots[i]); }

        int count = Mathf.Min(buttonsToPlace.Count, slots.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = buttonsToPlace[i];
            RectTransform rt = obj.GetComponent<RectTransform>();
            Vector2 basePos = slots[i];
            float offsetX = maxJitterX > 0f ? Random.Range(-maxJitterX, maxJitterX) : 0f;
            float offsetY = maxJitterY > 0f ? Random.Range(-maxJitterY, maxJitterY) : 0f;
            rt.SetParent(areaRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = basePos + new Vector2(offsetX, offsetY);
        }
    }
}