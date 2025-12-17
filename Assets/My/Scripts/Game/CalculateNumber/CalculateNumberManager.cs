using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// '수 계산하기(CalculateNumber)' 게임의 진행 관리 매니저.
/// 문제 출제, UI 배치, 정답 체크 로직을 담당.
/// </summary>
public class CalculateNumberManager : BaseGameManager<CalculateNumberSetting, CalculateNumberQuestion>
{
    [Header("--- Calculate Specific ---")]
    [SerializeField] private RectTransform questionImageRoot;   // 문제 이미지들이 생성될 부모 Transform
    [SerializeField] private GameObject questionImagePrefab;    // 문제 이미지 프리팹
    [SerializeField] private Transform leftQuestionZone;        // 왼쪽 문제 배치 구역
    [SerializeField] private Transform rightQuestionZone;       // 오른쪽 문제 배치 구역

    private int _foundAnswerCount; // 다중 정답 문제용 카운트
    
    // JSON 파일명 정의
    protected override string GetJsonFileName() => "CalculateNumber.json";
    // 문제 레벨 반환
    protected override int GetQuestionLevel(CalculateNumberQuestion question) => question.level;

    /// <summary>
    /// 부모 Initialize 과정에서 호출되는 자식 컴포넌트 초기화.
    /// 이미지 루트 하위 오브젝트 정리.
    /// </summary>
    protected override void OnSetupChildComponents()
    {
        if (questionImageRoot != null)
        {
            foreach (Transform child in questionImageRoot) Destroy(child.gameObject);
            questionImageRoot.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 게임 시작 로직.
    /// 선택된 레벨의 문제를 로드하고 첫 번째 문제를 설정.
    /// </summary>
    protected override void StartGameLogic()
    {   
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("BGM1");    
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
            else Debug.LogWarning($"Level {selectedLevel} Problems Not Found");
        }
    }
    
    /// <summary> 개별 문제 UI 설정 (텍스트, 이미지 배치). </summary>
    protected override void SetupSpecificQuestionUI(CalculateNumberQuestion q)
    {
        _foundAnswerCount = 0;
        UpdateProgressImage(q.level, currentQuestionIndex);
        
        // 좌우 랜덤 배치 결정
        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform contentParent = isTextLeft ? rightQuestionZone : leftQuestionZone;
    
        // 텍스트 설정
        if (questionTextObj && textParent)
        {
            questionTextObj.transform.SetParent(textParent, false);
            questionTextObj.text = q.questionText;
            questionTextObj.gameObject.SetActive(!string.IsNullOrEmpty(q.questionText));
        }
    
        // 기존 이미지 정리
        if (questionImageRoot)
        {
            foreach (Transform child in questionImageRoot) Destroy(child.gameObject);
            questionImageRoot.gameObject.SetActive(false);
        }
    
        // 이미지 생성 및 배치
        bool hasImages = q.questionImages != null && q.questionImages.Length > 0;
        if (hasImages)
        {
            if (questionImageRoot && questionImagePrefab && contentParent)
            {
                questionImageRoot.SetParent(contentParent, false);
                questionImageRoot.gameObject.SetActive(true);

                foreach (var imgSetting in q.questionImages)
                {
                    if (imgSetting == null) continue;
                    GameObject newImgObj = Instantiate(questionImagePrefab, questionImageRoot);
                    newImgObj.SetActive(true);

                    if (UIManager.Instance != null) UIManager.Instance.SetImageObj(newImgObj, imgSetting);
    
                    // 페이드 효과 적용
                    if (imgSetting.useFade)
                    {
                        Image imgComp = newImgObj.GetComponent<Image>();
                        if (imgComp) HandleImageFadeAsync(imgComp, imgSetting, newImgObj.GetCancellationTokenOnDestroy()).Forget();
                    }
                }
            }
        }
    }
    
    /// <summary> 정답 버튼 설정 및 배치. </summary>
    protected override void SetupAnswerButtons(CalculateNumberQuestion q)
    {
        List<string> options = new List<string>();
        if (q.correctAnswers != null) options.AddRange(q.correctAnswers);
        if (q.wrongAnswers != null)
        {
            int remaining = 4 - options.Count;
            if (remaining > 0) options.AddRange(q.wrongAnswers.Take(remaining));
        }
        
        // 보기 셔플
        options = options.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledBtns = answerButtons.OrderBy(x => Random.value).ToList();
    
        // 버튼 스타일 오버라이드용 스프라이트 로드
        Sprite overridePressed = null;
        if (q.buttonStyleOverride != null && q.buttonStyleOverride.useOverride)
            overridePressed = LoadSpriteFromStreamingAssets(q.buttonStyleOverride.pressedImageName);
    
        // 버튼 배치 (활성화된 버튼만 균등 분할)
        List<GameObject> activeBtns = shuffledBtns.Take(options.Count).ToList();
        int half = Mathf.CeilToInt(activeBtns.Count / 2f);
        PlaceButtonsInArea(activeBtns.Take(half).ToList(), leftAreaRect);
        PlaceButtonsInArea(activeBtns.Skip(half).ToList(), rightAreaRect);
    
        // 버튼 데이터 매핑
        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = shuffledBtns[i];
            Button btn = btnObj.GetComponent<Button>();
            Image btnImage = btnObj.GetComponent<Image>();
            
            btn.interactable = true;
            btn.onClick.RemoveAllListeners();

            if (i < options.Count)
            {
                string text = options[i];
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp) tmp.text = text;

                // 스타일 적용 (오버라이드 또는 기본 복구)
                if (q.buttonStyleOverride != null && q.buttonStyleOverride.useOverride)
                    ApplyButtonOverride(btn, btnImage, q.buttonStyleOverride);
                else
                    RestoreButtonDefault(btn, btnImage);

                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj, overridePressed));
                btnObj.SetActive(true);
            }
            else btnObj.SetActive(false);
        }
    }
    
    /// <summary> 정답 버튼 클릭 핸들러. </summary>
    private void OnAnswerClicked(string text, GameObject btnObj, Sprite pressedSprite)
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
                if (currentQuestion.correctAnswers.Contains(text)) { isCorrect = true; isLevelClear = true; }
                break;
            case QuestionType.MultipleChoice:
                if (currentQuestion.correctAnswers.Contains(text))
                {   
                    isCorrect = true;
                    _foundAnswerCount++;
                    if (_foundAnswerCount >= currentQuestion.correctAnswers.Length) isLevelClear = true;
                }
                break;
        }

        if (isCorrect)
        {   
            // 정답 시 눌림 이미지 적용 (설정된 경우)
            if (pressedSprite && btnObj.TryGetComponent(out Image img)) img.sprite = pressedSprite;
            btnObj.GetComponent<Button>().interactable = false;
            
            if (isLevelClear) HandleCorrectAnswer();
        }
        else HandleWrongAnswer();
    }
    
    /// <summary> 버튼 스타일 오버라이드 적용 (이미지 교체, SpriteSwap 전환 등). </summary>
    private void ApplyButtonOverride(Button btn, Image btnImage, ButtonOverrideSetting set)
    {
        Sprite normal = LoadSpriteFromStreamingAssets(set.normalImageName);
        if(normal && btnImage) { btnImage.sprite = normal; btnImage.color = set.buttonColor; }
        
        // 오버라이드 시 그라데이션 비활성화
        ImageGlobalGradient gradient = btn.GetComponent<ImageGlobalGradient>();
        if (gradient) gradient.enabled = false;
        
        // 눌림 상태 스프라이트 설정
        if (btn && !string.IsNullOrEmpty(set.pressedImageName))
        {
            Sprite pressed = LoadSpriteFromStreamingAssets(set.pressedImageName);
            if (pressed)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                SpriteState newState = new SpriteState { pressedSprite = pressed, highlightedSprite = normal, selectedSprite = normal, disabledSprite = pressed };
                btn.spriteState = newState;
            }
        }
    }
}