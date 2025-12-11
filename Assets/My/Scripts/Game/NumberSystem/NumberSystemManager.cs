using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video; 
using Random = UnityEngine.Random;

#region Data Structures (NumberSystem 전용)

[Serializable]
public class NumberSystemQuestion
{
    public int level;
    public QuestionType type; // SingleChoice, MultipleChoice, Sequence
    public string questionText;
    
    // 정답 및 오답 텍스트 데이터
    public string[] correctAnswers;
    public string[] wrongAnswers;
    
    // 문제에 표시될 이미지/비디오 (옵션)
    public ImageSetting questionImage; 
    public VideoSetting questionVideo; 
    
    // [신규 기능] 답변 텍스트별로 매칭될 이미지 (레벨1 등에서 사용)
    // 예: answerText="A"일 때 imagePath="Image/Apple.png" 매핑
    public AnswerImagePair[] answerImages;
}

[Serializable]
public class AnswerImagePair
{
    public string answerText; // 매핑할 답변 텍스트 (correctAnswers/wrongAnswers에 있는 값)
    public string imagePath;  // StreamingAssets 내부 경로
}

[Serializable]
public class NumberSystemSetting
{
    public NumberSystemQuestion[] questions;
    
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

public class NumberSystemManager : MonoBehaviour
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
    
    [Header("Special Question Objects")]
    [SerializeField] private RawImage questionRawImageObj; 
    [SerializeField] private VideoPlayer questionVideoPlayer;

    [Header("Buttons")]
    [SerializeField] private GameObject[] answerButtons; // 정답 버튼들 (최대 4개 가정)
    [SerializeField] private Button backButton;

    [Header("Result UI")]
    [SerializeField] private GameObject pageCorrect;
    [SerializeField] private Image imageCorrect;
    [SerializeField] private GameObject pageWrong;
    [SerializeField] private Image imageWrong;
    [SerializeField] private Button buttonRetry;
    [SerializeField] private Button buttonGameEnd;

    private NumberSystemSetting setting;
    private List<NumberSystemQuestion> _currentLevelQuestions;
    private int _currentQuestionIndex = 0;
    private int _totalQuestions = 4;
    private NumberSystemQuestion _currentQuestion;
    
    private bool _isProcessing = false;

    // 진행 상태 변수
    private int _currentSequenceIndex = 0;      
    private int _foundAnswerCount = 0;
    private HashSet<string> _foundAnswersSet; 

    // 버튼 초기 상태 복구용
    private Sprite _defaultButtonSprite; 
    private Color _defaultButtonColor;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // UI 초기화
        if (pageCorrect != null) pageCorrect.SetActive(false);
        if (pageWrong != null) pageWrong.SetActive(false);
        if (questionRawImageObj != null) questionRawImageObj.gameObject.SetActive(false);

        // 버튼 리스너
        if (buttonRetry != null) { buttonRetry.onClick.RemoveAllListeners(); buttonRetry.onClick.AddListener(OnRetryClicked); }
        if (buttonGameEnd != null) { buttonGameEnd.onClick.RemoveAllListeners(); buttonGameEnd.onClick.AddListener(OnGameEndClicked); }
        if (backButton != null) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => SceneManager.LoadScene("LevelSelect")); }

        // 데이터 로드
        LoadGameData();
        if (setting == null) return;
        ApplyUISettings(); 

        int selectedLevel = LevelSelectContext.SelectedLevel;
        if (selectedLevel <= 0) selectedLevel = 1;

        ApplyLevelImages(selectedLevel);
        ApplyButtonStyles();
        ApplyButtonGradients(selectedLevel);

        // 버튼 기본 상태 저장
        if (answerButtons != null && answerButtons.Length > 0)
        {
            Image firstImg = answerButtons[0].GetComponent<Image>();
            if (firstImg != null)
            {
                _defaultButtonSprite = firstImg.sprite;
                _defaultButtonColor = firstImg.color;
            }
        }

        // 문제 로드
        if (setting.questions != null)
        {
            var levelQuestions = setting.questions.Where(q => q.level == selectedLevel).ToList();
            if (levelQuestions.Count > 0)
            {
                int count = Mathf.Min(levelQuestions.Count, _totalQuestions);
                _currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
                _totalQuestions = _currentLevelQuestions.Count;
                _currentQuestionIndex = 0;
                SetQuestion(_currentQuestionIndex);
            }
        }
    }

    private void LoadGameData()
    {
        if (JsonLoader.Instance != null)
            setting = JsonLoader.Instance.LoadJsonData<NumberSystemSetting>("JSON/NumberSystem.json");
    }

    private void SetQuestion(int index)
    {
        if (index >= _currentLevelQuestions.Count) return;

        _isProcessing = false;
        _currentQuestion = _currentLevelQuestions[index];
        _currentSequenceIndex = 0;
        _foundAnswerCount = 0;
        _foundAnswersSet = new HashSet<string>();

        UpdateProgressImage(LevelSelectContext.SelectedLevel, index);

        // UI 리셋
        if (questionImageObj != null) questionImageObj.gameObject.SetActive(false);
        if (questionRawImageObj != null) questionRawImageObj.gameObject.SetActive(false);
        if (questionVideoPlayer != null) questionVideoPlayer.Stop();

        // 텍스트 설정
        if (questionTextObj != null)
        {
            questionTextObj.text = _currentQuestion.questionText;
            questionTextObj.gameObject.SetActive(true);
            TextGlobalGradient gradient = questionTextObj.GetComponent<TextGlobalGradient>();
            if (gradient != null && gradient.enabled) gradient.ApplyGradient();
        }

        // 미디어 설정
        if (_currentQuestion.questionVideo != null && !string.IsNullOrEmpty(_currentQuestion.questionVideo.fileName))
        {
            PlayVideo(_currentQuestion.questionVideo);
        }
        else if (_currentQuestion.questionImage != null && !string.IsNullOrEmpty(_currentQuestion.questionImage.sourceImage))
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetImageObj(questionImageObj.gameObject, _currentQuestion.questionImage);
                questionImageObj.gameObject.SetActive(true);
            }
        }

        SetupButtons(_currentQuestion);
    }

    private void PlayVideo(VideoSetting videoSetting)
    {
        if (questionVideoPlayer == null || questionRawImageObj == null) return;
        string fullPath = Path.Combine(Application.streamingAssetsPath, videoSetting.fileName).Replace("\\", "/");
        if (File.Exists(fullPath))
        {
            questionVideoPlayer.url = fullPath;
            questionVideoPlayer.isLooping = true; 
            questionVideoPlayer.Play();
            if (videoSetting.size != Vector2.zero) questionRawImageObj.rectTransform.sizeDelta = videoSetting.size;
            questionRawImageObj.gameObject.SetActive(true);
        }
    }

    private void SetupButtons(NumberSystemQuestion q)
    {
        List<string> options = new List<string>();
        if (q.correctAnswers != null) options.AddRange(q.correctAnswers);
        if (q.wrongAnswers != null)
        {
            int slotsRemaining = 4 - options.Count;
            if (slotsRemaining > 0) options.AddRange(q.wrongAnswers.Take(slotsRemaining));
        }

        // 보기 섞기
        options = options.OrderBy(x => Random.value).ToList();
        
        // 버튼 섞어서 배치
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();
        PlaceButtonsInArea(shuffledButtons.Take(2).ToList(), leftAreaRect);
        PlaceButtonsInArea(shuffledButtons.Skip(2).Take(2).ToList(), rightAreaRect);

        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            Button btn = btnObj.GetComponent<Button>();
            Image btnImage = btnObj.GetComponent<Image>();
            btn.interactable = true;

            // 버튼 초기화 (기본 상태 복구)
            if (btnImage != null && _defaultButtonSprite != null)
            {
                btnImage.sprite = _defaultButtonSprite;
                btnImage.color = _defaultButtonColor; // 그라데이션 적용을 위해 흰색이어야 함
            }
            // 그라데이션 활성화
            ImageGlobalGradient gradient = btnObj.GetComponent<ImageGlobalGradient>();
            if (gradient != null) gradient.enabled = true;

            if (i < options.Count)
            {
                string text = options[i];
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                
                // [신규 로직] 해당 텍스트에 매핑된 이미지가 있는지 확인
                string mappedImagePath = GetImagePathForAnswer(q, text);
                
                if (!string.IsNullOrEmpty(mappedImagePath))
                {
                    // 이미지가 있으면 텍스트는 숨기고 이미지를 버튼 배경으로 설정
                    if (tmp != null) tmp.text = "";
                    Sprite customSprite = LoadSpriteFromStreamingAssets(mappedImagePath);
                    if (customSprite != null && btnImage != null)
                    {
                        btnImage.sprite = customSprite;
                        btnImage.color = Color.white; // 이미지가 보이도록 흰색
                        if (gradient != null) gradient.enabled = false; // 이미지 본연 색상을 위해 그라데이션 끔
                    }
                }
                else
                {
                    // 이미지가 없으면 텍스트 표시
                    if (tmp != null) 
                    {
                        tmp.text = text;
                        TextGlobalGradient textGradient = tmp.GetComponent<TextGlobalGradient>();
                        if (textGradient != null && textGradient.enabled) textGradient.ApplyGradient();
                    }
                }

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
                btnObj.SetActive(true);
            }
            else
            {
                btnObj.SetActive(false); 
            }
        }
    }

    private string GetImagePathForAnswer(NumberSystemQuestion q, string answerText)
    {
        if (q.answerImages == null) return null;
        foreach (var pair in q.answerImages)
        {
            if (pair.answerText == answerText) return pair.imagePath;
        }
        return null;
    }

    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        if (_isProcessing) return;

        bool isCorrect = false;
        bool isLevelComplete = false;

        switch (_currentQuestion.type)
        {
            case QuestionType.SingleChoice:
                if (_currentQuestion.correctAnswers.Contains(clickedText))
                {
                    isCorrect = true;
                    isLevelComplete = true; 
                }
                break;

            case QuestionType.MultipleChoice:
                if (_currentQuestion.correctAnswers.Contains(clickedText))
                {
                    if (!_foundAnswersSet.Contains(clickedText))
                    {
                        isCorrect = true;
                        _foundAnswersSet.Add(clickedText);
                        _foundAnswerCount++;
                        btnObj.GetComponent<Button>().interactable = false; 
                        
                        if (_foundAnswerCount >= _currentQuestion.correctAnswers.Length)
                            isLevelComplete = true;
                    }
                }
                break;

            case QuestionType.Sequence:
                if (_currentSequenceIndex < _currentQuestion.correctAnswers.Length)
                {
                    string targetAnswer = _currentQuestion.correctAnswers[_currentSequenceIndex];
                    if (clickedText == targetAnswer)
                    {
                        isCorrect = true;
                        _currentSequenceIndex++;
                        btnObj.GetComponent<Button>().interactable = false;

                        if (_currentSequenceIndex >= _currentQuestion.correctAnswers.Length)
                            isLevelComplete = true;
                    }
                }
                break;
        }

        if (isCorrect)
        {
            if (isLevelComplete)
            {
                Debug.Log("Question Clear!");
                HandleCorrectAnswer().Forget();
            }
        }
        else
        {
            Debug.Log("Wrong!");
            HandleWrongAnswer();
        }
    }

    // --- Common Helpers ---
    private Sprite LoadSpriteFromStreamingAssets(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        string path = Path.Combine(Application.streamingAssetsPath, fileName).Replace("\\", "/");
        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData)) 
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        return null;
    }

    private void HandleWrongAnswer()
    {
        _isProcessing = true;
        if (pageWrong != null) pageWrong.SetActive(true);
    }

    private async UniTaskVoid HandleCorrectAnswer()
    {
        _isProcessing = true;
        if (pageCorrect != null) pageCorrect.SetActive(true);
        await UniTask.Delay(TimeSpan.FromSeconds(2));
        if (pageCorrect != null) pageCorrect.SetActive(false);

        _currentQuestionIndex++;
        if (_currentQuestionIndex >= _totalQuestions)
        {
            GameResultContext.CorrectCount = _totalQuestions;
            SceneManager.LoadScene("GameEnd");
        }
        else
        {
            SetQuestion(_currentQuestionIndex);
        }
    }

    private void OnRetryClicked()
    {
        if (pageWrong != null) pageWrong.SetActive(false);
        SetQuestion(_currentQuestionIndex);
    }

    private void OnGameEndClicked()
    {
        GameResultContext.CorrectCount = _currentQuestionIndex;
        SceneManager.LoadScene("GameEnd");
    }

    // --- UI Applying Helpers (Copy from others) ---
    private void UpdateProgressImage(int level, int questionIndex)
    {
        if (progressImage == null || setting == null || setting.levelProgresses == null) return;
        int levelIdx = level - 1;
        if (levelIdx < 0 || levelIdx >= setting.levelProgresses.Length) return;
        var stepSettings = setting.levelProgresses[levelIdx].steps;
        if (questionIndex >= 0 && questionIndex < stepSettings.Length)
        {
            UIManager.Instance.SetImageObj(progressImage.gameObject, stepSettings[questionIndex]);
            progressImage.gameObject.SetActive(true);
        }
    }

    private void PlaceButtonsInArea(List<GameObject> buttonsToPlace, RectTransform areaRect)
    {
        // (CalculateNumberManager의 PlaceButtonsInArea 로직 동일하게 사용)
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

    private void ApplyUISettings()
    {
        if (setting == null || UIManager.Instance == null) return;
        
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
    private void ApplyLevelImages(int level)
    {
        if (setting == null || UIManager.Instance == null) return;
        
        int index = level - 1;

        if (levelImage != null && setting.levelImages != null && index < setting.levelImages.Length)
        {
            UIManager.Instance.SetImageObj(levelImage.gameObject, setting.levelImages[index]);
            levelImage.gameObject.SetActive(true);
        }

        if (gameTypeImage != null && setting.gameTypeImages != null && index < setting.gameTypeImages.Length)
        {
            UIManager.Instance.SetImageObj(gameTypeImage.gameObject, setting.gameTypeImages[index]);
            gameTypeImage.gameObject.SetActive(true);
        }
    }
    private void ApplyButtonStyles()
    {
        if (JsonLoader.Instance != null && UIManager.Instance != null)
        {
            Settings globalSettings = JsonLoader.Instance.LoadJsonData<Settings>("Settings.json");
            if (globalSettings != null && globalSettings.questionButton != null)
            {
                foreach (GameObject btn in answerButtons)
                {
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
    private void ApplyButtonGradients(int level)
    {
        if (JsonLoader.Instance == null) return;

        LevelSetting levelSetting = JsonLoader.Instance.LoadJsonData<LevelSetting>("JSON/LevelSetting.json");
        
        if (levelSetting == null || levelSetting.levelGradients == null) return;

        int index = level - 1;
        if (index < 0 || index >= levelSetting.levelGradients.Length)
        {
            Debug.LogWarning($"[CalculateNumberManager] No gradient data for Level {level}");
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
}