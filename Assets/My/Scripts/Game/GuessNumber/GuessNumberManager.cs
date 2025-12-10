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

[Serializable]
public class LevelProgressSetting
{
    public ImageSetting[] steps;
}

[Serializable]
public class GuessNumberSetting
{
    public GuessNumberQuestion[] questions;
    public ImageSetting[] levelImages;
    public ImageSetting[] gameTypeImages;
    public LevelProgressSetting[] levelProgresses; 
    public ButtonSetting backButton;
    public float buttonMargin = 20f;
    
    public ImageSetting correctImage;
    public ImageSetting wrongImage;
    public ButtonSetting retryButton;
    public ButtonSetting gameEndButton;
}

#endregion

public class GuessNumberManager : MonoBehaviour
{
    [Header("Top UI")]
    [SerializeField] private Image levelImage;
    [SerializeField] private Image gameTypeImage;
    [SerializeField] private Image progressImage;

    [Header("Layout Areas")]
    [SerializeField] private RectTransform leftAreaRect;
    [SerializeField] private RectTransform rightAreaRect;
    
    private float buttonMargin = 20f; 

    [Header("Question UI Objects")]
    [SerializeField] private TextMeshProUGUI questionTextObj; 
    [SerializeField] private Image questionImageObj;       

    [Header("Question Zones")]
    [SerializeField] private Transform leftQuestionZone;
    [SerializeField] private Transform rightQuestionZone;

    [Header("Buttons")]
    [SerializeField] private GameObject[] answerButtons;
    [SerializeField] private Button backButton;
    
    [Header("Result UI Objects")]
    [SerializeField] private GameObject pageCorrect;
    [SerializeField] private Image imageCorrect;      
    
    [SerializeField] private GameObject pageWrong;    
    [SerializeField] private Image imageWrong;        
    [SerializeField] private Button buttonRetry;      
    [SerializeField] private Button buttonGameEnd;    

    private GuessNumberSetting setting;
    
    private List<GuessNumberQuestion> _currentLevelQuestions;
    private int _currentQuestionIndex;
    private int _totalQuestions = 4; 

    private GuessNumberQuestion _currentQuestion;
    private List<string> _remainingCorrectAnswers;
    private int _sequenceIndex;
    
    // [추가] 중복 클릭 방지 플래그
    private bool _isProcessing = false;

    private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // 1. 시작 시 결과 페이지 비활성화
        if (pageCorrect != null) pageCorrect.SetActive(false);
        if (pageWrong != null) pageWrong.SetActive(false);

        // 2. 오답 페이지 버튼 이벤트 연결
        if (buttonRetry != null)
        {
            buttonRetry.onClick.RemoveAllListeners();
            buttonRetry.onClick.AddListener(OnRetryClicked);
        }

        if (buttonGameEnd != null)
        {
            buttonGameEnd.onClick.RemoveAllListeners();
            buttonGameEnd.onClick.AddListener(OnGameEndClicked);
        }

        LoadGameData();

        if (setting == null)
        {
            Debug.LogError("[GuessNumberManager] Data Load Failed.");
            return;
        }

        ApplyUISettings();

        int selectedLevel = LevelSelectContext.SelectedLevel;
        if (selectedLevel <= 0) selectedLevel = 1;
        
        ApplyLevelImages(selectedLevel);
        ApplyButtonGradients(selectedLevel);

        if (backButton)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => SceneManager.LoadScene("LevelSelect"));
        }

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

            int count = Mathf.Min(levelQuestions.Count, _totalQuestions);
            _currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
            _totalQuestions = _currentLevelQuestions.Count;
            _currentQuestionIndex = 0;
            
            SetQuestion(_currentQuestionIndex);
        }
    }

    // ... (LoadGameData, ApplyUISettings, ApplyLevelImages, ApplyButtonGradients, ApplyGradientToImage, ApplyGradientToTarget, UpdateProgressImage 등은 기존과 동일하므로 생략하지 않고 전체 코드 유지를 위해 아래에 포함합니다.)

    private void LoadGameData()
    {
        if (JsonLoader.Instance != null)
        {
            setting = JsonLoader.Instance.LoadJsonData<GuessNumberSetting>("JSON/GuessNumber.json");
        }
    }

    private void ApplyUISettings()
    {
        if (UIManager.Instance == null) return;

        if (setting != null)
        {
            buttonMargin = setting.buttonMargin;

            if (backButton && setting.backButton != null)
            {
                UIManager.Instance.SetButtonObj(backButton.gameObject, setting.backButton).Forget();
            }

            if (imageCorrect != null && setting.correctImage != null)
                UIManager.Instance.SetImageObj(imageCorrect.gameObject, setting.correctImage);

            if (imageWrong != null && setting.wrongImage != null)
                UIManager.Instance.SetImageObj(imageWrong.gameObject, setting.wrongImage);

            if (buttonRetry != null && setting.retryButton != null)
                UIManager.Instance.SetButtonObj(buttonRetry.gameObject, setting.retryButton).Forget();

            if (buttonGameEnd != null && setting.gameEndButton != null)
                UIManager.Instance.SetButtonObj(buttonGameEnd.gameObject, setting.gameEndButton).Forget();
        }

        if (JsonLoader.Instance != null)
        {
            Settings globalSettings = JsonLoader.Instance.LoadJsonData<Settings>("Settings.json"); // 경로 주의

            if (globalSettings != null && globalSettings.questionButton != null)
            {
                if (answerButtons != null)
                {
                    foreach (GameObject btnObj in answerButtons)
                    {
                        if (btnObj == null) continue;

                        UIManager.Instance.SetButtonObj(btnObj, globalSettings.questionButton).Forget();

                        if (globalSettings.questionButton.buttonText != null)
                        {
                            TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                            if (tmp != null)
                            {
                                tmp.enableAutoSizing = true;
                                tmp.fontSizeMax = globalSettings.questionButton.buttonText.fontSize;
                                tmp.fontSizeMin = globalSettings.questionButton.buttonText.fontSize * 0.4f;
                                tmp.enableWordWrapping = true; 
                                tmp.margin = new Vector4(20, 10, 20, 10); 
                            }
                        }
                    }
                }
            }
        }
    }

    private void ApplyLevelImages(int level)
    {
        if (setting == null || UIManager.Instance == null) return;

        int index = level - 1;

        if (levelImage != null && setting.levelImages != null && index < setting.levelImages.Length)
        {
            var imgData = setting.levelImages[index];
            if (imgData != null)
            {
                UIManager.Instance.SetImageObj(levelImage.gameObject, imgData);
                levelImage.gameObject.SetActive(true);
            }
        }

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
    
    private void ApplyButtonGradients(int level)
    {
        if (JsonLoader.Instance == null) return;

        LevelSetting levelSetting = JsonLoader.Instance.LoadJsonData<LevelSetting>("JSON/LevelSetting.json");
        
        if (levelSetting == null || levelSetting.levelGradients == null) return;

        int index = level - 1;
        if (index < 0 || index >= levelSetting.levelGradients.Length)
        {
            Debug.LogWarning($"[GuessNumberManager] No gradient data for Level {level}");
            return;
        }

        GradientData data = levelSetting.levelGradients[index];

        ApplyGradientToTarget(questionTextObj, data);

        if (answerButtons != null)
        {
            foreach (var btnObj in answerButtons)
            {
                if (btnObj == null) continue;
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                ApplyGradientToTarget(tmp, data);

                Image btnImage = btnObj.GetComponent<Image>();
                ApplyGradientToImage(btnImage, data);
            }
        }
    }
    
    private void ApplyGradientToImage(Image targetImage, GradientData data)
    {
        if (targetImage == null || data == null) return;

        targetImage.color = Color.white;

        ImageGlobalGradient gradient = UIManager.GetOrAdd<ImageGlobalGradient>(targetImage.gameObject);
        
        if (gradient != null)
        {
            Color[] colors = new Color[] { data.topLeft, data.topRight, data.bottomRight, data.bottomLeft };
            int offset = Random.Range(0, 4);
            Color newTL = colors[(0 + offset) % 4];
            Color newTR = colors[(1 + offset) % 4];
            Color newBR = colors[(2 + offset) % 4];
            Color newBL = colors[(3 + offset) % 4];

            gradient.SetGradient(newTL, newTR, newBL, newBR);
            gradient.enabled = true;
        }
    }
    
    private void ApplyGradientToTarget(TextMeshProUGUI tmp, GradientData data)
    {
        if (tmp == null || data == null) return;

        tmp.enableVertexGradient = false;
        tmp.color = Color.white;

        TextGlobalGradient gradient = UIManager.GetOrAdd<TextGlobalGradient>(tmp.gameObject);
        if (gradient != null)
        {
            gradient.SetGradient(data.topLeft, data.topRight, data.bottomLeft, data.bottomRight);
            gradient.enabled = true; 
            gradient.ApplyGradient(); 
        }
    }

    private void UpdateProgressImage(int level, int questionIndex)
    {
        if (progressImage == null || setting == null || setting.levelProgresses == null) return;

        int levelIdx = level - 1;
        if (levelIdx < 0 || levelIdx >= setting.levelProgresses.Length) return;

        var stepSettings = setting.levelProgresses[levelIdx].steps;
        if (stepSettings == null) return;

        if (questionIndex >= 0 && questionIndex < stepSettings.Length)
        {
            var stepData = stepSettings[questionIndex];
            if (stepData != null)
            {
                UIManager.Instance.SetImageObj(progressImage.gameObject, stepData);
                progressImage.gameObject.SetActive(true);
            }
        }
    }

    private void SetQuestion(int index)
    {
        // 범위를 벗어난 경우 체크는 HandleCorrectAnswer에서 이미 처리하지만 안전장치로 유지
        if (index >= _currentLevelQuestions.Count) return; 
        
        // 문제 셋업 시작 시 플래그 해제
        _isProcessing = false;

        UpdateProgressImage(LevelSelectContext.SelectedLevel, index);

        _currentQuestion = _currentLevelQuestions[index];
        _remainingCorrectAnswers = new List<string>(_currentQuestion.correctAnswers);
        _sequenceIndex = 0;

        bool isTextLeft = Random.Range(0, 2) == 0;
        
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform imageParent = isTextLeft ? rightQuestionZone : leftQuestionZone;

        if (questionTextObj != null && textParent != null)
        {
            questionTextObj.transform.SetParent(textParent, false);
            bool hasText = !string.IsNullOrEmpty(_currentQuestion.questionText);
            
            if (hasText)
            {
                questionTextObj.text = _currentQuestion.questionText;
                TextGlobalGradient gradient = questionTextObj.GetComponent<TextGlobalGradient>();
                if (gradient != null && gradient.enabled)
                {
                    gradient.ApplyGradient();
                }
                questionTextObj.gameObject.SetActive(true);
            }
            else
            {
                questionTextObj.gameObject.SetActive(false);
            }
            textParent.gameObject.SetActive(hasText);
        }

        if (questionImageObj != null && imageParent != null)
        {
            questionImageObj.transform.SetParent(imageParent, false);
            bool hasImage = _currentQuestion.questionImage != null && !string.IsNullOrEmpty(_currentQuestion.questionImage.sourceImage);

            if (hasImage && UIManager.Instance != null)
            {
                UIManager.Instance.SetImageObj(questionImageObj.gameObject, _currentQuestion.questionImage);
                questionImageObj.gameObject.SetActive(true);
                imageParent.gameObject.SetActive(true);
            }
            else
            {
                questionImageObj.gameObject.SetActive(false);
                imageParent.gameObject.SetActive(false);
            }
        }

        SetupAndPlaceButtons(_currentQuestion);
    }

    private void SetupAndPlaceButtons(GuessNumberQuestion q)
    {
        int totalSlots = 4;
        List<string> displayTexts = new List<string>();

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
            if (tmp) 
            {
                tmp.text = text;
                TextGlobalGradient gradient = tmp.GetComponent<TextGlobalGradient>();
                if (gradient != null && gradient.enabled)
                {
                    gradient.ApplyGradient();
                }
            }

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            if (!string.IsNullOrEmpty(text))
                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
            
            btnObj.SetActive(!string.IsNullOrEmpty(text));
        }
    }

    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        // 연출 진행 중이거나 오답창이 떠있으면 입력 무시
        if (_isProcessing) return;

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
            // 문제가 완전히 클리어되었을 때
            if (isLevelClear)
            {
                // 비동기 처리(연출 및 대기)를 위해 Forget 호출
                HandleCorrectAnswer().Forget();
            }
        }
        else
        {
            Debug.Log("Wrong!");
            // 오답 페이지 활성화 및 플래그 설정
            _isProcessing = true;
            if (pageWrong != null) pageWrong.SetActive(true);
        }
    }

    /// <summary> 정답 처리 비동기 메서드 </summary>
    private async UniTaskVoid HandleCorrectAnswer()
    {
        _isProcessing = true;

        // 1. Page_Correct 활성화
        if (pageCorrect != null) pageCorrect.SetActive(true);

        // 2. 1초 대기
        await UniTask.Delay(TimeSpan.FromSeconds(1));

        // 3. Page_Correct 비활성화
        if (pageCorrect != null) pageCorrect.SetActive(false);

        // 4. 다음 문제 또는 게임 종료 판단
        _currentQuestionIndex++;
        if (_currentQuestionIndex >= _totalQuestions) // 마지막 문제였다면
        {   
            GameResultContext.CorrectCount = _totalQuestions;
            SceneManager.LoadScene("GameEnd");
        }
        else
        {
            // 다음 문제 출제 (SetQuestion 내부에서 _isProcessing = false 처리됨)
            SetQuestion(_currentQuestionIndex);
        }
    }

    /// <summary> Retry 버튼 클릭 이벤트 </summary>
    private void OnRetryClicked()
    {
        // 오답 페이지 닫기
        if (pageWrong != null) pageWrong.SetActive(false);
        
        // 현재 문제 다시 세팅 (버튼 활성화 및 셔플 리셋)
        SetQuestion(_currentQuestionIndex);
    }

    /// <summary> [추가] GameEnd 버튼 클릭 이벤트 </summary>
    private void OnGameEndClicked()
    {
        GameResultContext.CorrectCount = _currentQuestionIndex;
        SceneManager.LoadScene("GameEnd");
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