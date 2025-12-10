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

#region Data Structures (CalculateNumber 전용)

[Serializable]
public class CalculateNumberQuestion
{
    public int level;
    public int type;
    public string questionText;
    public string[] correctAnswers;
    public string[] wrongAnswers;
    
    public ImageSetting questionImage; 
    public VideoSetting questionVideo; 
}

[Serializable]
public class CalculateNumberSetting
{
    public CalculateNumberQuestion[] questions;
    
    public ImageSetting[] levelImages;       // 레벨별 상단 타이틀
    public ImageSetting[] gameTypeImages;    // 레벨별 게임 타입 아이콘
    
    public ButtonSetting backButton;
    public float buttonMargin = 20f; 
    
    public ImageSetting correctImage;
    public ImageSetting wrongImage;
    public ButtonSetting retryButton;
    public ButtonSetting gameEndButton;
}

#endregion

public class CalculateNumberManager : MonoBehaviour
{
    [Header("Top UI")]
    [SerializeField] private Image levelImage;
    [SerializeField] private Image gameTypeImage;
    [SerializeField] private Image progressImage; // (필요 시 구현)

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
    [SerializeField] private GameObject starPuzzleObject; 

    [Header("Buttons")]
    [SerializeField] private GameObject[] answerButtons;

    [Header("Result UI")]
    [SerializeField] private GameObject pageCorrect;
    [SerializeField] private GameObject pageWrong;
    [SerializeField] private Button buttonRetry;
    [SerializeField] private Button buttonGameEnd;

    private CalculateNumberSetting _setting;
    private List<CalculateNumberQuestion> _currentLevelQuestions;
    private int _currentQuestionIndex = 0;
    private int _totalQuestions = 4;
    private CalculateNumberQuestion _currentQuestion;
    
    private bool _isProcessing = false;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // 1. UI 초기화
        if (pageCorrect != null) pageCorrect.SetActive(false);
        if (pageWrong != null) pageWrong.SetActive(false);
        
        if (starPuzzleObject != null) starPuzzleObject.SetActive(false);
        if (questionRawImageObj != null) questionRawImageObj.gameObject.SetActive(false);

        // 2. 버튼 리스너 연결
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
        
        // 3. 데이터 로드
        LoadGameData();
        ApplyUISettings(); 

        int selectedLevel = LevelSelectContext.SelectedLevel;
        if (selectedLevel <= 0) selectedLevel = 1;

        // 4. 레벨 이미지 및 게임 타입 이미지 적용
        ApplyLevelHeader(selectedLevel);
        ApplyButtonStyles();
        
        // [추가됨] 레벨별 그라데이션 색상 적용
        ApplyButtonGradients(selectedLevel);

        if (_setting != null && _setting.questions != null)
        {
            var levelQuestions = _setting.questions
                .Where(q => q.level == selectedLevel)
                .ToList();

            if (levelQuestions.Count > 0)
            {
                int count = Mathf.Min(levelQuestions.Count, _totalQuestions);
                _currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
                _totalQuestions = _currentLevelQuestions.Count;
                _currentQuestionIndex = 0;

                SetQuestion(_currentQuestionIndex);
            }
            else
            {
                Debug.LogWarning($"Level {selectedLevel}에 해당하는 문제가 없습니다.");
            }
        }
    }

    private void LoadGameData()
    {
        if (JsonLoader.Instance != null)
        {
            _setting = JsonLoader.Instance.LoadJsonData<CalculateNumberSetting>("JSON/CalculateNumber.json");
        }
    }

    private void SetQuestion(int index)
    {
        if (index >= _currentLevelQuestions.Count) return;

        _isProcessing = false;
        _currentQuestion = _currentLevelQuestions[index];

        // UI 요소 초기화
        if (questionImageObj != null) questionImageObj.gameObject.SetActive(false);
        if (questionRawImageObj != null) questionRawImageObj.gameObject.SetActive(false);
        if (questionVideoPlayer != null) questionVideoPlayer.Stop();
        if (starPuzzleObject != null) starPuzzleObject.SetActive(false);

        // 텍스트 설정
        if (questionTextObj != null)
        {
            questionTextObj.text = _currentQuestion.questionText;
            questionTextObj.gameObject.SetActive(true);
            
            // [추가] 텍스트 변경 후 그라데이션 재적용 (Mesh 갱신)
            TextGlobalGradient gradient = questionTextObj.GetComponent<TextGlobalGradient>();
            if (gradient != null && gradient.enabled)
            {
                gradient.ApplyGradient();
            }
        }

        // 콘텐츠 타입별 활성화
        if (starPuzzleObject != null && _currentQuestion.questionText.Contains("별"))
        {
            starPuzzleObject.SetActive(true);
        }
        else if (_currentQuestion.questionVideo != null && !string.IsNullOrEmpty(_currentQuestion.questionVideo.fileName))
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
            
            if (videoSetting.size != Vector2.zero) 
                questionRawImageObj.rectTransform.sizeDelta = videoSetting.size;

            questionRawImageObj.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[CalculateNumberManager] Video file not found: {fullPath}");
        }
    }

    private void SetupButtons(CalculateNumberQuestion q)
    {
        List<string> options = new List<string>();
        if (q.correctAnswers != null && q.correctAnswers.Length > 0)
            options.Add(q.correctAnswers[0]);
        
        if (q.wrongAnswers != null)
            options.AddRange(q.wrongAnswers.Take(3));

        options = options.OrderBy(x => Random.value).ToList();
        
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();

        PlaceButtonsInArea(shuffledButtons.Take(2).ToList(), leftAreaRect);
        PlaceButtonsInArea(shuffledButtons.Skip(2).Take(2).ToList(), rightAreaRect);

        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            
            if (i < options.Count)
            {
                string text = options[i];
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) 
                {
                    tmp.text = text;
                    // [추가] 버튼 텍스트 변경 후 그라데이션 재적용
                    TextGlobalGradient gradient = tmp.GetComponent<TextGlobalGradient>();
                    if (gradient != null && gradient.enabled)
                    {
                        gradient.ApplyGradient();
                    }
                }

                Button btn = btnObj.GetComponent<Button>();
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

    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        if (_isProcessing) return;

        bool isCorrect = false;
        if (_currentQuestion.correctAnswers.Contains(clickedText))
        {
            isCorrect = true;
        }

        if (isCorrect)
        {
            Debug.Log("Correct!");
            HandleCorrectAnswer().Forget();
        }
        else
        {
            Debug.Log("Wrong!");
            _isProcessing = true;
            if (pageWrong != null) pageWrong.SetActive(true);
        }
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

    // --- Helper Functions ---

    // [추가] 레벨별 그라데이션 적용 로직
    private void ApplyButtonGradients(int level)
    {
        if (JsonLoader.Instance == null) return;

        // LevelSetting.json 로드
        LevelSetting levelSetting = JsonLoader.Instance.LoadJsonData<LevelSetting>("JSON/LevelSetting.json");
        
        if (levelSetting == null || levelSetting.levelGradients == null) return;

        int index = level - 1;
        if (index < 0 || index >= levelSetting.levelGradients.Length)
        {
            Debug.LogWarning($"[CalculateNumberManager] No gradient data for Level {level}");
            // 범위를 벗어난 경우 기본값(첫 번째나 마지막)을 쓸 수도 있지만 여기서는 리턴
            if (levelSetting.levelGradients.Length > 0) index = 0;
            else return;
        }

        GradientData data = levelSetting.levelGradients[index];

        // 1. 문제 텍스트에 그라데이션 적용
        ApplyGradientToTarget(questionTextObj, data);

        // 2. 정답 버튼들에 그라데이션 적용
        if (answerButtons != null)
        {
            foreach (var btnObj in answerButtons)
            {
                if (btnObj == null) continue;

                // A. 텍스트 그라데이션
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                ApplyGradientToTarget(tmp, data);

                // B. 버튼 배경 이미지 그라데이션 (랜덤 회전)
                Image btnImage = btnObj.GetComponent<Image>();
                ApplyGradientToImage(btnImage, data);
            }
        }
    }
    
    // [추가] 이미지 그라데이션 적용 (랜덤 회전 포함)
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
    
    // [추가] 텍스트 그라데이션 적용
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
        {
            for (int col = 0; col < columns; col++)
            {
                float x = -halfAreaSize.x + cellWidth * (col + 0.5f);
                float y =  halfAreaSize.y - cellHeight * (row + 0.5f);
                slots.Add(new Vector2(x, y));
            }
        }

        for (int i = slots.Count - 1; i > 0; i--) 
        { 
            int j = Random.Range(0, i + 1); 
            (slots[i], slots[j]) = (slots[j], slots[i]); 
        }

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
        if (_setting == null || UIManager.Instance == null) return;
        
        this.buttonMargin = _setting.buttonMargin;

        if (pageCorrect != null && _setting.correctImage != null)
        {
            var img = pageCorrect.GetComponentInChildren<Image>();
            if(img) UIManager.Instance.SetImageObj(img.gameObject, _setting.correctImage);
        }
        if (pageWrong != null && _setting.wrongImage != null)
        {
            var img = pageWrong.GetComponentInChildren<Image>();
            if(img) UIManager.Instance.SetImageObj(img.gameObject, _setting.wrongImage);
        }

        if (buttonRetry != null && _setting.retryButton != null)
            UIManager.Instance.SetButtonObj(buttonRetry.gameObject, _setting.retryButton).Forget();
        if (buttonGameEnd != null && _setting.gameEndButton != null)
            UIManager.Instance.SetButtonObj(buttonGameEnd.gameObject, _setting.gameEndButton).Forget();
    }

    private void ApplyLevelHeader(int level)
    {
        if (_setting == null || UIManager.Instance == null) return;
        
        int index = level - 1;

        if (levelImage != null && _setting.levelImages != null && index < _setting.levelImages.Length)
        {
            UIManager.Instance.SetImageObj(levelImage.gameObject, _setting.levelImages[index]);
            levelImage.gameObject.SetActive(true);
        }

        if (gameTypeImage != null && _setting.gameTypeImages != null && index < _setting.gameTypeImages.Length)
        {
            UIManager.Instance.SetImageObj(gameTypeImage.gameObject, _setting.gameTypeImages[index]);
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
}