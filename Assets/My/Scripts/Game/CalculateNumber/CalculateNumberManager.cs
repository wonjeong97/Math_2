using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
    public QuestionType type;
    public string questionText;
    public string[] correctAnswers;
    public string[] wrongAnswers;
    public ImageSetting[] questionImages;
    public VideoSetting questionVideo;
    public ButtonOverrideSetting buttonStyleOverride;
}

[Serializable]
public class ButtonOverrideSetting
{
    public bool useOverride;

    [Header("Images (StreamingAssets Path)")]
    public string normalImageName;

    public string pressedImageName;
    public Color buttonColor = Color.white;
}

[Serializable]
public class CalculateNumberSetting
{
    public CalculateNumberQuestion[] questions;
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

public class CalculateNumberManager : MonoBehaviour
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
    [SerializeField] private RectTransform questionImageRoot; // 이미지들이 생성될 부모 Transform
    [SerializeField] private GameObject questionImagePrefab; // 복제해서 쓸 기본 이미지 프리팹

    [Header("Question Zones")] 
    [SerializeField] private Transform leftQuestionZone;
    [SerializeField] private Transform rightQuestionZone;

    [Header("Video Question Object")] 
    [SerializeField] private GameObject questionVideoObject;
    
    private RawImage _questionRawImage;
    private VideoPlayer _questionVideoPlayer;

    [Header("Buttons")]
    [SerializeField] private GameObject[] answerButtons;
    [SerializeField] private Button backButton;

    [Header("Result UI")]
    [SerializeField] private GameObject pageCorrect;
    [SerializeField] private Image imageCorrect;

    [SerializeField] private GameObject pageWrong;
    [SerializeField] private Image imageWrong;
    [SerializeField] private Button buttonRetry;
    [SerializeField] private Button buttonGameEnd;

    private CalculateNumberSetting setting;
    private List<CalculateNumberQuestion> _currentLevelQuestions;
    private int _currentQuestionIndex = 0;
    private int _totalQuestions = 4;
    private CalculateNumberQuestion _currentQuestion;

    private bool _isProcessing = false;

    private int _currentSequenceIndex = 0;
    private int _foundAnswerCount = 0;

    private Sprite _defaultButtonSprite;
    private SpriteState _defaultSpriteState;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // 1. UI 초기화
        if (pageCorrect != null) pageCorrect.SetActive(false);
        if (pageWrong != null) pageWrong.SetActive(false);

        // 비디오 오브젝트 초기화 및 컴포넌트 가져오기
        if (questionVideoObject != null)
        {
            _questionRawImage = questionVideoObject.GetComponent<RawImage>();
            _questionVideoPlayer = questionVideoObject.GetComponent<VideoPlayer>();

            if (_questionRawImage == null) Debug.LogWarning("questionVideoObject에 RawImage 컴포넌트가 없습니다.");
            if (_questionVideoPlayer == null) Debug.LogWarning("questionVideoObject에 VideoPlayer 컴포넌트가 없습니다.");

            questionVideoObject.SetActive(false);
        }

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

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => SceneManager.LoadScene("LevelSelect"));
        }

        // 3. 데이터 로드
        LoadGameData();

        if (setting == null)
        {
            Debug.LogError("[CalculateNumberManager] Data Load Failed.");
            return;
        }

        ApplyUISettings();

        int selectedLevel = LevelSelectContext.SelectedLevel;
        if (selectedLevel <= 0) selectedLevel = 1;

        // 4. 상단 이미지 및 스타일 적용
        ApplyLevelImages(selectedLevel);
        ApplyButtonStyles();
        ApplyButtonGradients(selectedLevel);

        // 기본 버튼 상태 저장
        if (answerButtons != null && answerButtons.Length > 0)
        {
            Button firstBtn = answerButtons[0].GetComponent<Button>();
            Image firstImg = answerButtons[0].GetComponent<Image>();
            if (firstImg != null) _defaultButtonSprite = firstImg.sprite;
            if (firstBtn != null) _defaultSpriteState = firstBtn.spriteState;
        }

        // 6. 문제 로드 및 시작
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
            setting = JsonLoader.Instance.LoadJsonData<CalculateNumberSetting>("JSON/CalculateNumber.json");
        }
    }

    private void SetQuestion(int index)
    {
        if (index >= _currentLevelQuestions.Count) return;

        _isProcessing = false;
        UpdateProgressImage(LevelSelectContext.SelectedLevel, index);
        
        _currentQuestion = _currentLevelQuestions[index];
        _currentSequenceIndex = 0;
        _foundAnswerCount = 0;
        
        // --- 1. 좌우 랜덤 배치 결정 ---
        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform contentParent = isTextLeft ? rightQuestionZone : leftQuestionZone;

        // --- 2. UI 리셋 ---
        if (questionVideoObject != null)
        {
            if (_questionVideoPlayer != null) _questionVideoPlayer.Stop();
            questionVideoObject.SetActive(false);
        }

        // 기존 이미지 제거
        if (questionImageRoot != null)
        {
            foreach (Transform child in questionImageRoot) Destroy(child.gameObject);
            questionImageRoot.gameObject.SetActive(false);
        }

        // --- 3. 텍스트 설정 ---
        if (questionTextObj != null && textParent != null)
        {
            questionTextObj.transform.SetParent(textParent, false);
            if (!string.IsNullOrEmpty(_currentQuestion.questionText))
            {
                questionTextObj.text = _currentQuestion.questionText;
                questionTextObj.gameObject.SetActive(true);
                TextGlobalGradient gradient = questionTextObj.GetComponent<TextGlobalGradient>();
                if (gradient != null && gradient.enabled) gradient.ApplyGradient();
            }
            else
            {
                questionTextObj.gameObject.SetActive(false);
            }
        }

        // --- 4. 콘텐츠(비디오/이미지) 설정 ---
        bool hasVideo = _currentQuestion.questionVideo != null && !string.IsNullOrEmpty(_currentQuestion.questionVideo.fileName);
        bool hasImages = _currentQuestion.questionImages != null && _currentQuestion.questionImages.Length > 0;

        if (hasVideo)
        {
            if (questionVideoObject != null && contentParent != null)
            {
                questionVideoObject.transform.SetParent(contentParent, false);
            }
            PlayVideo(_currentQuestion.questionVideo);
        }
        else if (hasImages)
        {
            if (questionImageRoot != null && questionImagePrefab != null && contentParent != null)
            {
                questionImageRoot.SetParent(contentParent, false);
                questionImageRoot.gameObject.SetActive(true);

                // 이미지 배열 순회하며 생성 및 효과 적용
                foreach (var imgSetting in _currentQuestion.questionImages)
                {
                    if (imgSetting == null) continue;

                    GameObject newImgObj = Instantiate(questionImagePrefab, questionImageRoot);
                    newImgObj.SetActive(true);

                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.SetImageObj(newImgObj, imgSetting);
                    }

                    // 페이드 설정이 켜져있다면 효과 실행
                    if (imgSetting.useFade)
                    {
                        Image imgComp = newImgObj.GetComponent<Image>();
                        if (imgComp != null)
                        {
                            // 오브젝트가 파괴될 때 취소되도록 토큰 전달
                            HandleImageFadeAsync(imgComp, imgSetting, newImgObj.GetCancellationTokenOnDestroy()).Forget();
                        }
                    }
                }
            }
        }

        SetupButtons(_currentQuestion);
    }
    
    /// <summary> 개별 이미지 페이드 효과 처리 (반복 지원) </summary>
    private async UniTaskVoid HandleImageFadeAsync(Image target, ImageSetting setting, CancellationToken token)
    {
        float duration = setting.fadeDuration > 0 ? setting.fadeDuration : 1f;
        
        // isFadeOut이 true면 1(보임) -> 0(안보임)
        // isFadeOut이 false면 0(안보임) -> 1(보임)
        float startAlpha = setting.isFadeOut ? 1f : 0f;
        float endAlpha   = setting.isFadeOut ? 0f : 1f;

        // 초기 알파값 세팅
        SetAlpha(target, startAlpha);

        // 루프 처리 (loop가 true면 계속 왔다갔다, false면 1회만)
        do
        {
            // 1. Start -> End 로 페이드
            await FadeAlpha(target, startAlpha, endAlpha, duration, token);

            if (setting.loop)
            {
                // 반복 시: End -> Start 로 다시 페이드 (Ping-Pong 효과, 깜빡임)
                await FadeAlpha(target, endAlpha, startAlpha, duration, token);
            }
            else
            {
                break; // 반복 아니면 종료
            }

        } while (setting.loop && target != null);
    }
    
    private async UniTask FadeAlpha(Image target, float from, float to, float duration, CancellationToken token)
    {
        float time = 0f;
        while (time < duration)
        {
            token.ThrowIfCancellationRequested(); // 오브젝트 파괴 시 중단
            if (target == null) return;

            time += Time.deltaTime;
            float t = time / duration;
            float currentAlpha = Mathf.Lerp(from, to, t);
            SetAlpha(target, currentAlpha);
            
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
        SetAlpha(target, to); // 최종값 보정
    }

    private void SetAlpha(Image target, float alpha)
    {
        if (target == null) return;
        Color c = target.color;
        c.a = alpha;
        target.color = c;
    }

    private void PlayVideo(VideoSetting videoSetting)
    {
        if (questionVideoObject == null || _questionVideoPlayer == null || _questionRawImage == null) return;

        string fullPath = Path.Combine(Application.streamingAssetsPath, videoSetting.fileName).Replace("\\", "/");

        if (File.Exists(fullPath))
        {
            _questionVideoPlayer.url = fullPath;
            _questionVideoPlayer.isLooping = true;
            _questionVideoPlayer.Play();

            if (videoSetting.size != Vector2.zero)
                _questionRawImage.rectTransform.sizeDelta = videoSetting.size;

            questionVideoObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[CalculateNumberManager] Video file not found: {fullPath}");
        }
    }

    private void SetupButtons(CalculateNumberQuestion q)
    {
        List<string> options = new List<string>();
        if (q.correctAnswers != null && q.correctAnswers.Length > 0) options.AddRange(q.correctAnswers);
        if (q.wrongAnswers != null)
        {
            int slotsRemaining = 4 - options.Count;
            if (slotsRemaining > 0) options.AddRange(q.wrongAnswers.Take(slotsRemaining));
        }

        options = options.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();

        PlaceButtonsInArea(shuffledButtons.Take(2).ToList(), leftAreaRect);
        PlaceButtonsInArea(shuffledButtons.Skip(2).Take(2).ToList(), rightAreaRect);

        bool isOverride = q.buttonStyleOverride != null && q.buttonStyleOverride.useOverride;

        Sprite overridePressedSprite = null;
        if (isOverride)
        {
            overridePressedSprite = LoadSpriteFromStreamingAssets(q.buttonStyleOverride.pressedImageName);
        }

        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            Button btn = btnObj.GetComponent<Button>();
            btn.interactable = true;

            if (i < options.Count)
            {
                string text = options[i];
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = text;
                    TextGlobalGradient gradient = tmp.GetComponent<TextGlobalGradient>();
                    if (gradient != null && gradient.enabled) gradient.ApplyGradient();
                }

                Image btnImage = btnObj.GetComponent<Image>();

                if (isOverride) ApplyButtonOverride(btn, btnImage, q.buttonStyleOverride);
                else RestoreButtonDefault(btn, btnImage);

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj, overridePressedSprite));

                btnObj.SetActive(true);
            }
            else
            {
                btnObj.SetActive(false);
            }
        }
    }

    private void ApplyButtonOverride(Button btn, Image btnImage, ButtonOverrideSetting setting)
    {
        Sprite normalSprite = LoadSpriteFromStreamingAssets(setting.normalImageName);
        Sprite pressedSprite = LoadSpriteFromStreamingAssets(setting.pressedImageName);

        if (normalSprite != null && btnImage != null)
        {
            btnImage.sprite = normalSprite;
            btnImage.color = setting.buttonColor;
        }

        if (pressedSprite != null && btn != null)
        {
            btn.transition = Selectable.Transition.SpriteSwap;
            SpriteState newState = new SpriteState();
            newState.pressedSprite = pressedSprite;
            newState.highlightedSprite = normalSprite;
            newState.selectedSprite = normalSprite;
            newState.disabledSprite = pressedSprite;
            btn.spriteState = newState;
        }

        ImageGlobalGradient gradient = btn.GetComponent<ImageGlobalGradient>();
        if (gradient != null) gradient.enabled = false;
    }

    private void RestoreButtonDefault(Button btn, Image btnImage)
    {
        if (_defaultButtonSprite != null && btnImage != null)
        {
            btnImage.sprite = _defaultButtonSprite;
            btnImage.color = Color.white;
        }

        if (btn != null)
        {
            btn.spriteState = _defaultSpriteState;
        }

        ImageGlobalGradient gradient = btn.GetComponent<ImageGlobalGradient>();
        if (gradient != null) gradient.enabled = true;
    }

    private Sprite LoadSpriteFromStreamingAssets(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;

        string path = Path.Combine(Application.streamingAssetsPath, fileName).Replace("\\", "/");

        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {   
                texture.wrapMode = TextureWrapMode.Clamp; 
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
        else
        {
            Debug.LogWarning($"[CalculateNumberManager] Image not found: {path}");
        }

        return null;
    }

    private void OnAnswerClicked(string clickedText, GameObject btnObj, Sprite pressedSpriteOverride)
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
                    isCorrect = true;
                    _foundAnswerCount++;
                    if (_foundAnswerCount >= _currentQuestion.correctAnswers.Length)
                    {
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
                        if (_currentSequenceIndex >= _currentQuestion.correctAnswers.Length)
                        {
                            isLevelComplete = true;
                        }
                    }
                }

                break;
        }

        if (isCorrect)
        {
            Button btn = btnObj.GetComponent<Button>();
            Image btnImage = btnObj.GetComponent<Image>();

            if (pressedSpriteOverride != null && btnImage != null)
            {
                btnImage.sprite = pressedSpriteOverride;
            }

            if (btn != null) btn.interactable = false;

            if (isLevelComplete)
            {
                Debug.Log("Question Clear!");
                HandleCorrectAnswer().Forget();
            }
            else
            {
                Debug.Log("Keep Going...");
            }
        }
        else
        {
            Debug.Log("Wrong!");
            HandleWrongAnswer();
        }
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
                float y = halfAreaSize.y - cellHeight * (row + 0.5f);
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
        if (setting == null || UIManager.Instance == null) return;

        this.buttonMargin = setting.buttonMargin;

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
}