using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using Random = UnityEngine.Random;

/// <summary>
/// 모든 미니게임 매니저의 공통 기능을 담당하는 부모 클래스
/// TSetting: 설정 데이터 클래스 (예: CalculateNumberSetting)
/// TQuestion: 개별 문제 데이터 클래스 (예: CalculateNumberQuestion)
/// </summary>
public abstract class BaseGameManager<TSetting, TQuestion> : MonoBehaviour 
    where TSetting : class 
    where TQuestion : class
{
    [Header("--- Base UI References ---")]
    [SerializeField] protected Image levelImage;
    [SerializeField] protected Image gameTypeImage;
    [SerializeField] protected Image progressImage;
    [SerializeField] protected Button backButton;
    [SerializeField] protected Image fadeImage;

    [Header("--- Base Question UI ---")]
    [SerializeField] protected TextMeshProUGUI questionTextObj;
    [SerializeField] protected Image questionImageObj; // 단일 이미지용 (필요시 사용)
    
    [Header("--- Base Video UI ---")]
    [SerializeField] protected GameObject questionVideoObject; 
    protected RawImage _questionRawImage;
    protected VideoPlayer _questionVideoPlayer;

    [Header("--- Base Buttons & Areas ---")]
    [SerializeField] protected GameObject[] answerButtons;
    [SerializeField] protected RectTransform leftAreaRect;
    [SerializeField] protected RectTransform rightAreaRect;
    protected float buttonMargin = 20f;

    [Header("--- Base Result UI ---")]
    [SerializeField] protected GameObject pageCorrect;
    [SerializeField] protected Image imageCorrect;
    [SerializeField] protected GameObject pageWrong;
    [SerializeField] protected Image imageWrong;
    [SerializeField] protected Button buttonRetry;
    [SerializeField] protected Button buttonGameEnd;

    // --- Data & State ---
    protected TSetting currentSetting;
    protected List<TQuestion> currentLevelQuestions;
    protected int currentQuestionIndex = 0;
    protected int totalQuestions = 4;
    protected TQuestion currentQuestion;
    protected bool isProcessing = false;

    // 버튼 복구용 캐싱
    protected Sprite defaultButtonSprite;
    protected Color defaultButtonColor;
    protected Vector2 defaultButtonSize;

    // --- Abstract Methods ---
    protected abstract string GetJsonFileName();
    protected abstract int GetQuestionLevel(TQuestion question);
    protected abstract void SetupSpecificQuestionUI(TQuestion question); // 문제별 UI 세팅 (텍스트, 이미지 등)
    protected abstract void SetupAnswerButtons(TQuestion question); // 정답 버튼 세팅

    protected virtual void Start()
    {
        Initialize();
    }

    protected virtual void Initialize()
    {
        // 1. UI 초기화
        if (pageCorrect) pageCorrect.SetActive(false);
        if (pageWrong) pageWrong.SetActive(false);
        if (questionImageObj) questionImageObj.gameObject.SetActive(false);
        
        // 비디오 초기화
        if (questionVideoObject)
        {
            _questionRawImage = questionVideoObject.GetComponent<RawImage>();
            _questionVideoPlayer = questionVideoObject.GetComponent<VideoPlayer>();
            questionVideoObject.SetActive(false);
        }

        // 2. 버튼 리스너 연결
        if (buttonRetry) { buttonRetry.onClick.RemoveAllListeners(); buttonRetry.onClick.AddListener(OnRetryClicked); }
        if (buttonGameEnd) { buttonGameEnd.onClick.RemoveAllListeners(); buttonGameEnd.onClick.AddListener(OnGameEndClicked); }
        if (backButton) { backButton.onClick.RemoveAllListeners(); backButton.onClick.AddListener(() => SceneManager.LoadScene("LevelSelect")); }

        // 3. 버튼 기본 상태 저장
        if (answerButtons != null && answerButtons.Length > 0)
        {
            Image img = answerButtons[0].GetComponent<Image>();
            RectTransform rt = answerButtons[0].GetComponent<RectTransform>();
            if (img) { defaultButtonSprite = img.sprite; defaultButtonColor = img.color; }
            if (rt) defaultButtonSize = rt.sizeDelta;
        }

        // 4. 데이터 로드
        LoadGameData();
        
        if (currentSetting == null)
        {
            Debug.LogError($"[{this.GetType().Name}] Data Load Failed.");
            return;
        }

        // 공통 UI/스타일 적용
        ApplyButtonGradients(LevelSelectContext.SelectedLevel);

        // 5. 문제 필터링 및 시작
        StartGameLogic();
        
        // 6. 씬 진입 페이드 인
        if (fadeImage != null && FadeManager.Instance != null)
        {
            // 1초 동안 페이드 인 (Alpha 1 -> 0)
            FadeManager.Instance.FadeIn(fadeImage, 1.0f).Forget();
        }
    }

    protected void LoadGameData()
    {
        if (JsonLoader.Instance != null)
        {
            currentSetting = JsonLoader.Instance.LoadJsonData<TSetting>($"JSON/{GetJsonFileName()}");
        }
    }

    protected virtual void StartGameLogic()
    {
        // 자식 클래스에서 override하여 questions 리스트를 가져오는 로직 구현 필요
        // (TSetting이 제네릭이라 questions 필드에 직접 접근이 어려우므로, 
        //  자식에서 _currentLevelQuestions를 채우고 SetQuestion(0) 호출 권장)
    }

    protected void SetQuestionBase(int index)
    {
        if (currentLevelQuestions == null || index >= currentLevelQuestions.Count) return;

        isProcessing = false;
        currentQuestionIndex = index;
        currentQuestion = currentLevelQuestions[index];

        // UI 리셋
        if (questionImageObj) questionImageObj.gameObject.SetActive(false);
        if (questionVideoObject)
        {
            if (_questionVideoPlayer) _questionVideoPlayer.Stop();
            questionVideoObject.SetActive(false);
        }

        // 진행도 업데이트 (Setting 구조가 제각각이라 추상화가 복잡하면 자식에게 위임 가능하지만, 여기선 생략)
        
        // 문제 UI 세팅 (자식 구현)
        SetupSpecificQuestionUI(currentQuestion);
        
        // 정답 버튼 세팅 (자식 구현)
        SetupAnswerButtons(currentQuestion);
    }

    // --- Media Helpers ---
    protected void PlayVideo(VideoSetting videoSetting)
    {
        if (!questionVideoObject || !_questionVideoPlayer || !_questionRawImage) return;

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
    }

    protected Sprite LoadSpriteFromStreamingAssets(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;
        string path = Path.Combine(Application.streamingAssetsPath, fileName).Replace("\\", "/");
        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {
                texture.wrapMode = TextureWrapMode.Clamp; // 중요: 1픽셀 잘림 방지
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
        }
        return null;
    }

    // --- Game Logic Flow ---
    protected virtual void HandleCorrectAnswer()
    {
        HandleCorrectAnswerAsync().Forget();
    }

    private async UniTaskVoid HandleCorrectAnswerAsync()
    {   
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Correct ({currentQuestionIndex + 1}/{totalQuestions})");
        isProcessing = true;
        if (pageCorrect) pageCorrect.SetActive(true);
        
        // 정답 페이지 보여주는 시간
        await UniTask.Delay(TimeSpan.FromSeconds(2));
        
        currentQuestionIndex++;
        
        // 마지막 문제인지 확인
        if (currentQuestionIndex >= totalQuestions)
        {
            // 마지막 문제라면 PageCorrect를 끄지 않고 유지한 채 페이드 아웃
            float fadeTime = 1.0f;
            if (JsonLoader.Instance != null && JsonLoader.Instance.settings != null)
            {
                fadeTime = JsonLoader.Instance.settings.fadeTime;
            }

            // 페이드 아웃
            if (fadeImage != null && FadeManager.Instance != null)
            {
                await FadeManager.Instance.FadeOut(fadeImage, fadeTime);
            }
            
            // 페이드 아웃 완료 후 게임 종료
            OnGameEndClicked();
        }
        else
        {
            // 다음 문제가 남았을 때만 PageCorrect 끄기
            if (pageCorrect) pageCorrect.SetActive(false);
            SetQuestionBase(currentQuestionIndex);
        }
    }

    protected virtual void HandleWrongAnswer()
    {
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Wrong ({currentQuestionIndex + 1}/{totalQuestions})");
        isProcessing = true;    
        if (pageWrong) pageWrong.SetActive(true);
    }

    protected virtual void OnRetryClicked()
    {   
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Retry ({currentQuestionIndex + 1}/{totalQuestions})");
        if (pageWrong) pageWrong.SetActive(false);
        SetQuestionBase(currentQuestionIndex);
    }

    protected virtual void OnGameEndClicked()
    {   
        Debug.Log($"[{SceneManager.GetActiveScene().name}] Player gave up ({currentQuestionIndex + 1}/{totalQuestions})");
        GameResultContext.CorrectCount = currentQuestionIndex; // 혹은 맞춘 개수 로직
        SceneManager.LoadScene("GameEnd");
    }

    // --- UI Helpers ---
    protected void ApplyButtonGradients(int level)
    {
        if (JsonLoader.Instance == null) return;
        LevelSetting levelSetting = JsonLoader.Instance.LoadJsonData<LevelSetting>("JSON/LevelSetting.json");
        if (levelSetting == null || levelSetting.levelGradients == null) return;

        int index = level - 1;
        if (index < 0 || index >= levelSetting.levelGradients.Length) return;

        GradientData data = levelSetting.levelGradients[index];

        if (questionTextObj) ApplyGradientToTarget(questionTextObj, data);

        if (answerButtons != null)
        {
            foreach (var btnObj in answerButtons)
            {
                if (!btnObj) continue;
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                ApplyGradientToTarget(tmp, data);
                Image btnImage = btnObj.GetComponent<Image>();
                ApplyGradientToImage(btnImage, data);
            }
        }
    }

    protected void ApplyGradientToImage(Image targetImage, GradientData data)
    {
        if (!targetImage || data == null) return;
        targetImage.color = Color.white;
        ImageGlobalGradient gradient = UIManager.GetOrAdd<ImageGlobalGradient>(targetImage.gameObject);
        if (gradient)
        {
            Color[] colors = { data.topLeft, data.topRight, data.bottomRight, data.bottomLeft };
            int offset = Random.Range(0, 4);
            gradient.SetGradient(
                colors[(0 + offset) % 4], colors[(1 + offset) % 4],
                colors[(3 + offset) % 4], colors[(2 + offset) % 4]); // 순서 주의
            gradient.enabled = true;
        }
    }

    protected void ApplyGradientToTarget(TextMeshProUGUI tmp, GradientData data)
    {
        if (!tmp || data == null) return;
        tmp.enableVertexGradient = false;
        tmp.color = Color.white;
        TextGlobalGradient gradient = UIManager.GetOrAdd<TextGlobalGradient>(tmp.gameObject);
        if (gradient)
        {
            gradient.SetGradient(data.topLeft, data.topRight, data.bottomLeft, data.bottomRight);
            gradient.enabled = true;
            gradient.ApplyGradient();
        }
    }

    protected void PlaceButtonsInArea(List<GameObject> buttonsToPlace, RectTransform areaRect)
    {
        if (!areaRect || buttonsToPlace == null || buttonsToPlace.Count == 0) return;

        Rect rect = areaRect.rect;
        Vector2 halfAreaSize = rect.size * 0.5f;
        const int columns = 1;
        const int rows = 2; 
        
        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;

        List<Vector2> slots = new List<Vector2>();
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
                slots.Add(new Vector2(
                    -halfAreaSize.x + cellWidth * (col + 0.5f),
                     halfAreaSize.y - cellHeight * (row + 0.5f)
                ));

        // Shuffle slots
        for (int i = slots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (slots[i], slots[j]) = (slots[j], slots[i]); }

        int count = Mathf.Min(buttonsToPlace.Count, slots.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = buttonsToPlace[i];
            RectTransform rt = obj.GetComponent<RectTransform>();
            
            // Jitter 계산
            Vector3 scale = rt.localScale;
            float w = rt.sizeDelta.x * scale.x;
            float h = rt.sizeDelta.y * scale.y;
            float jitterX = Mathf.Max(0f, (cellWidth - w) * 0.5f - buttonMargin);
            float jitterY = Mathf.Max(0f, (cellHeight - h) * 0.5f - buttonMargin);

            Vector2 basePos = slots[i];
            float offsetX = jitterX > 0 ? Random.Range(-jitterX, jitterX) : 0f;
            float offsetY = jitterY > 0 ? Random.Range(-jitterY, jitterY) : 0f;

            rt.SetParent(areaRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = basePos + new Vector2(offsetX, offsetY);
        }
    }
}