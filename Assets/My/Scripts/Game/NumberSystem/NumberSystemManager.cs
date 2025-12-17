using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// '수의 체계(NumberSystem)' 게임 관리 매니저.
/// 문제 출제, 정답 이미지 매핑, 게임 진행 상태를 관리.
/// </summary>
public class NumberSystemManager : BaseGameManager<NumberSystemSetting, NumberSystemQuestion>
{
    [Header("--- NumberSystem Specific ---")]
    [SerializeField] private Transform leftQuestionZone;    // 왼쪽 문제 배치 구역
    [SerializeField] private Transform rightQuestionZone;   // 오른쪽 문제 배치 구역

    private int _currentSequenceIndex;          // 순서 문제용 현재 인덱스
    private int _foundAnswerCount;              // 다중 선택 문제용 정답 카운트
    private HashSet<string> _foundAnswersSet;   // 이미 찾은 정답 기록 (중복 방지)

    // JSON 파일명
    protected override string GetJsonFileName() => "NumberSystem.json";
    // 문제 레벨
    protected override int GetQuestionLevel(NumberSystemQuestion q) => q.level;

    /// <summary>
    /// 게임 시작 로직.
    /// 레벨에 맞는 문제를 로드하고 셔플하여 첫 번째 문제를 출제.
    /// </summary>
    protected override void StartGameLogic()
    {   
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("BGM3");    
        }
        int selectedLevel = LevelSelectContext.SelectedLevel > 0 ? LevelSelectContext.SelectedLevel : 1;
        
        // BaseManager의 managerSetting 사용
        if (managerSetting?.questions != null)
        {
            var levelQuestions = managerSetting.questions.Where(q => q.level == selectedLevel).ToList();
            if (levelQuestions.Count > 0)
            {
                int count = Mathf.Min(levelQuestions.Count, totalQuestions);
                // 랜덤 셔플
                currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
                totalQuestions = currentLevelQuestions.Count;
                SetQuestionBase(0);
            }
            else Debug.LogWarning($"Level {selectedLevel} Problems Not Found");
        }
    }

    /// <summary> 개별 문제 UI 설정 (텍스트, 이미지 배치). </summary>
    protected override void SetupSpecificQuestionUI(NumberSystemQuestion q)
    {
        // 상태 초기화
        _currentSequenceIndex = 0;
        _foundAnswerCount = 0;
        _foundAnswersSet = new HashSet<string>();

        // 진행도 업데이트
        UpdateProgressImage(q.level, currentQuestionIndex);

        // 좌우 랜덤 배치
        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform contentParent = isTextLeft ? rightQuestionZone : leftQuestionZone;

        // 텍스트 설정
        if (questionTextObj && textParent)
        {
            questionTextObj.transform.SetParent(textParent, false);
            bool hasText = !string.IsNullOrEmpty(q.questionText);
            questionTextObj.text = hasText ? q.questionText : "";
            questionTextObj.gameObject.SetActive(hasText);
        }

        // 이미지 설정
        bool hasImage = q.questionImage != null && !string.IsNullOrEmpty(q.questionImage.sourceImage);
        if (hasImage && questionImageObj)
        {
            questionImageObj.transform.SetParent(contentParent, false);
            UIManager.Instance.SetImageObj(questionImageObj.gameObject, q.questionImage);
            questionImageObj.gameObject.SetActive(true);
        }
        else
        {
            if (questionImageObj) questionImageObj.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 정답 버튼 설정 및 배치.
    /// 텍스트에 대응하는 이미지가 있으면 교체(AnswerImagePair).
    /// </summary>
    protected override void SetupAnswerButtons(NumberSystemQuestion q)
    {
        List<string> options = new List<string>();
        if (q.correctAnswers != null) options.AddRange(q.correctAnswers);
        if (q.wrongAnswers != null)
        {
            int remaining = 4 - options.Count;
            if (remaining > 0) options.AddRange(q.wrongAnswers.Take(remaining));
        }

        // 셔플
        options = options.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();
        
        // 배치 (좌우 균등 분할)
        List<GameObject> activeBtns = shuffledButtons.Take(options.Count).ToList();
        int half = Mathf.CeilToInt(activeBtns.Count / 2f);
        PlaceButtonsInArea(activeBtns.Take(half).ToList(), leftAreaRect);
        PlaceButtonsInArea(activeBtns.Skip(half).ToList(), rightAreaRect);

        // 버튼 데이터 매핑
        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            Button btn = btnObj.GetComponent<Button>();
            Image btnImage = btnObj.GetComponent<Image>();
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            
            btn.interactable = true;
            btn.onClick.RemoveAllListeners();

            // 기본 상태 및 크기 복구
            RestoreButtonDefault(btn, btnImage, btnRect);

            if (i < options.Count)
            {
                string text = options[i];
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                
                // 텍스트에 매핑된 이미지 확인
                AnswerImagePair pair = GetAnswerImagePair(q, text);
                if (pair != null && !string.IsNullOrEmpty(pair.imagePath))
                {
                    // 이미지 모드: 텍스트 지우고 이미지 적용
                    if (tmp) tmp.text = "";
                    Sprite s = LoadSpriteFromStreamingAssets(pair.imagePath);
                    if (s && btnImage)
                    {
                        btnImage.sprite = s;
                        btnImage.color = Color.white;
                        
                        // 이미지는 그라데이션 제거
                        var gradient = btnObj.GetComponent<ImageGlobalGradient>();
                        if(gradient) gradient.enabled = false;
                        
                        // 설정된 크기 적용
                        if (pair.size != Vector2.zero && btnRect) btnRect.sizeDelta = pair.size;
                    }
                }
                else
                {
                    // 텍스트 모드
                    if (tmp) tmp.text = text;
                }

                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
                btnObj.SetActive(true);
            }
            else btnObj.SetActive(false);
        }
    }

    /// <summary> 정답 버튼 클릭 핸들러. </summary>
    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        if (isProcessing) return;
        bool isCorrect = false;
        bool isLevelClear = false;
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX("Button");    
        }
        
        switch (currentQuestion.type)
        {
            case QuestionType.SingleChoice:
                if (currentQuestion.correctAnswers.Contains(clickedText)) { isCorrect = true; isLevelClear = true; }
                break;
                
            case QuestionType.MultipleChoice:
                if (currentQuestion.correctAnswers.Contains(clickedText))
                {
                    // 중복 클릭 방지
                    if (!_foundAnswersSet.Contains(clickedText))
                    {
                        isCorrect = true;
                        _foundAnswersSet.Add(clickedText);
                        _foundAnswerCount++;
                        btnObj.SetActive(false); 
                        if (_foundAnswerCount >= currentQuestion.correctAnswers.Length) isLevelClear = true;
                    }
                }
                break;
                
            case QuestionType.Sequence:
                if (_currentSequenceIndex < currentQuestion.correctAnswers.Length)
                {
                    string target = currentQuestion.correctAnswers[_currentSequenceIndex];
                    if (clickedText == target)
                    {
                        isCorrect = true;
                        _currentSequenceIndex++;
                        btnObj.SetActive(false);
                        if (_currentSequenceIndex >= currentQuestion.correctAnswers.Length) isLevelClear = true;
                    }
                }
                break;
        }

        if (isCorrect)
        {
            if (isLevelClear) HandleCorrectAnswer();
        }
        else HandleWrongAnswer();
    }

    /// <summary> 정답 텍스트에 대응하는 이미지 정보(AnswerImagePair) 조회. </summary>
    private AnswerImagePair GetAnswerImagePair(NumberSystemQuestion q, string text)
    {
        if (q.answerImages == null) return null;
        return q.answerImages.FirstOrDefault(x => x.answerText == text);
    }
}