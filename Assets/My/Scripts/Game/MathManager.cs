using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MathManager : MonoBehaviour
{
    [Header("Question Texts")]
    [SerializeField] private TMP_Text leftQuestionText;   // 왼쪽 영역 문제 텍스트
    [SerializeField] private TMP_Text rightQuestionText;  // 오른쪽 영역 문제 텍스트

    [Header("Score UI")]
    [SerializeField] private TMP_Text scoreText;

    [Header("Answer Buttons")]
    [SerializeField] private GameObject[] answerButtonObjects;   // 5개 버튼 오브젝트

    [Header("Answer Areas (4 Panels)")]
    [SerializeField] private RectTransform[] answerAreaRects;    // 4개 정답 영역 패널

    [SerializeField] private float buttonMargin = 20f;           // 버튼과 슬롯 테두리 사이 여유
    
    [Header("Debug")] 
    [SerializeField] private TMP_Text debugText;

    private Button[] _answerButtons;
    private Image[] _answerButtonImages;

    private MathData _mathData;
    private int _currentQuestionIndex;
    private int _score;
    private int[] _currentCandidateAnswers;

    private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
    private const int AnswerCount = 16;
    
    private List<int> _questionOrder;
    private int _questionOrderIndex;

    private void Awake()
    {
        int count = answerButtonObjects.Length;
        _currentCandidateAnswers = new int[count];
        _answerButtons = new Button[count];
        _answerButtonImages = new Image[count];

        for (int i = 0; i < count; i++)
        {
            GameObject obj = answerButtonObjects[i];
            if (obj == null) continue;

            _answerButtons[i] = obj.GetComponentInChildren<Button>();
            _answerButtonImages[i] = obj.GetComponentInChildren<Image>();

            if (_answerButtons[i] == null)
            {
                Debug.LogWarning($"[MathManager] Awake-> Button component not found in {obj.name}");
                continue;
            }

            int index = i;
            _answerButtons[i].onClick.AddListener(() => OnClickAnswerButton(index));
        }
    }

    private void Start()
    {
        LoadMathData();
        _currentQuestionIndex = 0;
        _score = 0;
        RefreshScoreUI();

        InitializeQuestionOrder();
        ShowCurrentQuestion();
        
        int level = LevelSelectContext.SelectedLevel;
        GameType gameType = LevelSelectContext.SelectedGameType;
        
        debugText.SetText($"Level: {level}\n Type: {gameType}");
    }

    /// <summary> 문제 인덱스 순서를 랜덤하게 셔플 </summary>
    private void InitializeQuestionOrder()
    {
        _questionOrder = new List<int>();
        if (_mathData == null || _mathData.maths == null) return;

        int total = _mathData.maths.Length;
        for (int i = 0; i < total; i++)
            _questionOrder.Add(i);

        // Fisher-Yates shuffle
        for (int i = _questionOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_questionOrder[i], _questionOrder[j]) = (_questionOrder[j], _questionOrder[i]);
        }

        _questionOrderIndex = 0;
    }
    
    /// <summary> Math.json 데이터 로드 </summary>
    private void LoadMathData()
    {
        _mathData = JsonLoader.Instance.LoadJsonData<MathData>("JSON/Math.json");

        if (_mathData == null || _mathData.maths == null || _mathData.maths.Length == 0)
        {
            Debug.LogError("[MathManager] LoadMathData-> Math.json is empty or failed to load");
        }
    }

    /// <summary> 현재 인덱스의 문제를 화면에 표시 </summary>
    private void ShowCurrentQuestion()
    {
        if (_mathData == null || _mathData.maths == null || _mathData.maths.Length == 0)
            return;

        // 모든 문제를 다 풀면 새로 셔플
        if (_questionOrder == null || _questionOrderIndex >= _questionOrder.Count)
        {
            InitializeQuestionOrder();
        }

        if (_questionOrder != null)
        {
            int questionIdx = _questionOrder[_questionOrderIndex];
            MathSetting current = _mathData.maths[questionIdx];

            // 문제 텍스트 좌/우 랜덤 표시
            bool showOnLeft = Random.Range(0, 2) == 0;
            SetQuestionText(current.question, showOnLeft);

            // 버튼 레이아웃/이미지 세팅
            ApplyButtonLayout(current.answerButton);
            FillAnswerButtons(current);

            // 버튼 위치 랜덤 배치
            RandomizeButtonPositionsAcrossAreas();

            _currentQuestionIndex = questionIdx;
        }
    }

    /// <summary> 문제 텍스트를 좌/우 중 한쪽에만 표시 </summary>
    private void SetQuestionText(string text, bool showOnLeft)
    {
        if (leftQuestionText != null)
        {
            leftQuestionText.gameObject.SetActive(showOnLeft);
            if (showOnLeft)
                leftQuestionText.text = text;
        }

        if (rightQuestionText != null)
        {
            rightQuestionText.gameObject.SetActive(!showOnLeft);
            if (!showOnLeft)
                rightQuestionText.text = text;
        }
    }

    /// <summary> JSON에 정의된 버튼 사이즈/스케일을 모든 버튼에 적용 </summary>
    private void ApplyButtonLayout(ButtonSetting setting)
    {
        if (setting == null || answerButtonObjects == null)
            return;

        Vector2 size = setting.size;
        Vector3 scale = setting.scale == Vector3.zero ? Vector3.one : setting.scale;

        foreach (GameObject obj in answerButtonObjects)
        {
            if (obj == null) continue;

            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt == null) continue;

            if (size != Vector2.zero)
                rt.sizeDelta = size;

            rt.localScale = scale;
        }
    }

    /// <summary> 정답 1개 + 오답 4개를 랜덤 배치하고 버튼 이미지 설정 </summary>
    private void FillAnswerButtons(MathSetting current)
    {
        if (_answerButtons == null || _answerButtons.Length == 0)
            return;

        int buttonCount = _answerButtons.Length;
        int correctAnswer = current.answerIndex;

        int correctSlot = Random.Range(0, buttonCount);
        _currentCandidateAnswers[correctSlot] = correctAnswer;

        // 오답 풀 생성 (0~15 중 정답 제외)
        List<int> wrongPool = new List<int>();
        for (int i = 0; i < AnswerCount; i++)
        {
            if (i != correctAnswer)
                wrongPool.Add(i);
        }

        // 나머지 슬롯에 오답 배치
        for (int i = 0; i < buttonCount; i++)
        {
            if (i == correctSlot) continue;

            int randomIndex = Random.Range(0, wrongPool.Count);
            int wrongAnswer = wrongPool[randomIndex];
            wrongPool.RemoveAt(randomIndex);
            _currentCandidateAnswers[i] = wrongAnswer;
        }

        // 각 버튼의 이미지 설정
        for (int i = 0; i < buttonCount; i++)
        {
            Image img = _answerButtonImages[i];
            if (img == null) continue;

            int answerIndex = _currentCandidateAnswers[i];
            string customPath = null;

            // 정답 슬롯이면 JSON에 정의된 이미지 경로 우선 사용
            if (i == correctSlot && current.answerButton != null && current.answerButton.buttonBackgroundImage != null)
            {
                customPath = current.answerButton.buttonBackgroundImage.sourceImage;
            }

            Sprite sprite = LoadAnswerSprite(answerIndex, customPath);
            img.sprite = sprite;
        }
    }

    /// <summary> 버튼 클릭 시 정답 비교 후 다음 문제로 진행 </summary>
    private void OnClickAnswerButton(int buttonIndex)
    {
        if (_mathData == null || _mathData.maths == null || _mathData.maths.Length == 0)
            return;

        int chosen = _currentCandidateAnswers[buttonIndex];
        int correct = _mathData.maths[_currentQuestionIndex].answerIndex;

        if (chosen == correct)
        {
            _score++;
            RefreshScoreUI();
        }

        // 다음 문제로 진행 (랜덤 순서)
        _questionOrderIndex++;
        ShowCurrentQuestion();
    }

    /// <summary> 상단 스코어 텍스트 갱신 </summary>
    private void RefreshScoreUI()
    {
        if (scoreText != null)
            scoreText.text = _score.ToString();
    }

    /// <summary> StreamingAssets에서 버튼 스프라이트 로드 </summary>
    private Sprite LoadAnswerSprite(int answerIndex, string customPath)
    {
        string relativePath = string.IsNullOrEmpty(customPath)
            ? $"image/button/button_{answerIndex:D2}.png"
            : customPath;

        if (_spriteCache.TryGetValue(relativePath, out Sprite cached))
            return cached;

        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath).Replace("\\", "/");
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[MathManager] LoadAnswerSprite-> file not found: {fullPath}");
            return null;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

        _spriteCache[relativePath] = sprite;
        return sprite;
    }

    /// <summary>
    /// 5개 버튼을 4개의 정답 영역(answerAreaRects)에 랜덤으로 나눠 배치하고,
    /// 각 영역 내부에서는 격자 슬롯 + 약간의 랜덤 오프셋으로 겹치지 않게 배치.
    /// </summary>
    private void RandomizeButtonPositionsAcrossAreas()
    {
        if (answerButtonObjects == null || answerButtonObjects.Length == 0)
            return;

        if (answerAreaRects == null || answerAreaRects.Length == 0)
        {
            Debug.LogWarning("[MathManager] RandomizeButtonPositionsAcrossAreas-> answerAreaRects is null or empty");
            return;
        }

        int buttonCount = answerButtonObjects.Length;
        int areaCount = answerAreaRects.Length;

        // 영역별 버튼 인덱스 리스트
        List<int>[] areaButtonLists = new List<int>[areaCount];
        for (int i = 0; i < areaCount; i++)
        {
            areaButtonLists[i] = new List<int>();
        }

        // 우선 버튼 인덱스 전체 리스트 생성 및 셔플
        List<int> allButtonIndices = new List<int>();
        for (int i = 0; i < buttonCount; i++)
            allButtonIndices.Add(i);

        for (int i = allButtonIndices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (allButtonIndices[i], allButtonIndices[j]) = (allButtonIndices[j], allButtonIndices[i]);
        }

        // 버튼 수가 영역 수 이상이면, 각 영역에 최소 1개씩 먼저 배정
        int indexCursor = 0;
        if (buttonCount >= areaCount)
        {
            for (int a = 0; a < areaCount; a++)
            {
                if (indexCursor >= allButtonIndices.Count) break;
                int btnIdx = allButtonIndices[indexCursor++];
                areaButtonLists[a].Add(btnIdx);
            }
        }

        // 남은 버튼들은 아무 영역에나 랜덤 배정
        for (; indexCursor < allButtonIndices.Count; indexCursor++)
        {
            int btnIdx = allButtonIndices[indexCursor];
            int areaIdx = Random.Range(0, areaCount);
            areaButtonLists[areaIdx].Add(btnIdx);
        }

        // 각 영역 안에서 실제 배치
        for (int a = 0; a < areaCount; a++)
        {
            PlaceButtonsInArea(areaButtonLists[a], answerAreaRects[a]);
        }
    }

    /// <summary>
    /// 주어진 영역(RectTransform) 안에서, 주어진 버튼 인덱스들을
    /// 2x3 격자 슬롯 + 작은 랜덤 오프셋으로 배치 (슬롯 간에는 겹침 없음).
    /// </summary>
    private void PlaceButtonsInArea(List<int> buttonIndices, RectTransform areaRect)
    {
        if (areaRect == null)
            return;

        if (buttonIndices == null || buttonIndices.Count == 0)
            return;

        // 기준이 될 버튼 RectTransform
        GameObject sampleObj = answerButtonObjects[buttonIndices[0]];
        if (sampleObj == null)
            return;

        RectTransform sampleRt = sampleObj.GetComponent<RectTransform>();
        if (sampleRt == null)
            return;

        Rect rect = areaRect.rect;
        Vector2 halfAreaSize = rect.size * 0.5f;

        const int columns = 3;  // 한 영역 내에서 2열
        const int rows = 2;     // 최대 3행 -> 슬롯 6개 (버튼 5개까지 여유)

        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;

        // 버튼 실제 크기 계산
        Vector3 scale = sampleRt.localScale;
        float buttonWidth = sampleRt.sizeDelta.x * scale.x;
        float buttonHeight = sampleRt.sizeDelta.y * scale.y;

        // 슬롯 내에서의 최대 흔들기 범위 (이웃 슬롯과는 안 겹치게)
        float maxJitterX = Mathf.Max(0f, (cellWidth - buttonWidth) * 0.5f - buttonMargin);
        float maxJitterY = Mathf.Max(0f, (cellHeight - buttonHeight) * 0.5f - buttonMargin);

        // 슬롯 중심 좌표 리스트 (areaRect 로컬 좌표 기준)
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

        // 슬롯 순서 랜덤 셔플
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        int count = Mathf.Min(buttonIndices.Count, slots.Count);

        for (int i = 0; i < count; i++)
        {
            int buttonIdx = buttonIndices[i];
            GameObject obj = answerButtonObjects[buttonIdx];
            if (obj == null) continue;

            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt == null) continue;

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
