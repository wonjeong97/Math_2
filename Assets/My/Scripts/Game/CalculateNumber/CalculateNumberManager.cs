using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video; 
using TMPro;
using Random = UnityEngine.Random;

public class CalculateNumberManager : BaseGameManager<CalculateNumberSetting, CalculateNumberQuestion>
{
    [Header("--- Calculate Specific ---")]
    [SerializeField] private GameObject backgroundObj;
    [SerializeField] private RectTransform questionImageRoot;   
    [SerializeField] private GameObject questionImagePrefab;    
    [SerializeField] private Transform leftQuestionZone;        
    [SerializeField] private Transform rightQuestionZone;       

    private int _foundAnswerCount; 
    
    protected override string GetJsonFileName() => "CalculateNumber.json";
    protected override int GetQuestionLevel(CalculateNumberQuestion question) => question.level;

    protected override void OnSetupChildComponents()
    {   
        if (backgroundObj != null && managerSetting != null && managerSetting.backgroundImage != null && UIManager.Instance != null)
        {
            UIManager.Instance.SetImageObj(backgroundObj, managerSetting.backgroundImage, this.GetCancellationTokenOnDestroy()).Forget();
        }
        else if (UIManager.Instance == null)
        {
            Debug.LogError("[CalculateNumberManager] UIManager.Instance is null");
        }
        
        if (questionImageRoot != null)
        {
            foreach (Transform child in questionImageRoot) Destroy(child.gameObject);
            questionImageRoot.gameObject.SetActive(false);
        }
    }
    
    protected override void StartGameLogic()
    {   
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM("BGM1");    
        }
        int selectedLevel = LevelSelectContext.SelectedLevel > 0 ? LevelSelectContext.SelectedLevel : 1;
        
        if (managerSetting?.questions != null)
        {
            var levelQuestions = managerSetting.questions.Where(q => q.level == selectedLevel).ToList();
            if (levelQuestions.Count > 0)
            {
                int count = Mathf.Min(levelQuestions.Count, totalQuestions);
                currentLevelQuestions = levelQuestions.OrderBy(x => Random.value).Take(count).ToList();
                totalQuestions = currentLevelQuestions.Count;
                SetQuestionBase(0);
            }
            else Debug.LogWarning($"Level {selectedLevel} Problems Not Found");
        }
    }
    
    protected override void SetupSpecificQuestionUI(CalculateNumberQuestion q)
    {
        _foundAnswerCount = 0;
        UpdateProgressImage(q.level, currentQuestionIndex);
        
        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform contentParent = isTextLeft ? rightQuestionZone : leftQuestionZone;
    
        if (questionTextObj && textParent)
        {
            questionTextObj.transform.SetParent(textParent, false);
            questionTextObj.text = q.questionText;
            questionTextObj.gameObject.SetActive(!string.IsNullOrEmpty(q.questionText));
        }
    
        if (questionImageRoot)
        {
            foreach (Transform child in questionImageRoot) Destroy(child.gameObject);
            questionImageRoot.gameObject.SetActive(false);
        }
    
        bool hasImages = q.questionImages != null && q.questionImages.Length > 0;
        if (hasImages)
        {
            if (questionImageRoot && questionImagePrefab && contentParent)
            {
                questionImageRoot.SetParent(contentParent, false);
                questionImageRoot.anchorMin = Vector2.zero;
                questionImageRoot.anchorMax = Vector2.one; 
                questionImageRoot.offsetMin = Vector2.zero; 
                questionImageRoot.offsetMax = Vector2.zero; 
                
                questionImageRoot.gameObject.SetActive(true);

                foreach (var imgSetting in q.questionImages)
                {
                    if (imgSetting == null) continue;
                    GameObject newImgObj = Instantiate(questionImagePrefab, questionImageRoot);
                    newImgObj.SetActive(true);

                    if (UIManager.Instance != null) UIManager.Instance.SetImageObj(newImgObj, imgSetting).Forget();
    
                    if (imgSetting.useFade)
                    {
                        Image imgComp = newImgObj.GetComponent<Image>();
                        if (imgComp) HandleImageFadeAsync(imgComp, imgSetting, newImgObj.GetCancellationTokenOnDestroy()).Forget();
                    }
                }
            }
        }
    }
    
    protected override void SetupAnswerButtons(CalculateNumberQuestion q)
    {
        List<string> options = new List<string>();
        if (q.correctAnswers != null) options.AddRange(q.correctAnswers);
        if (q.wrongAnswers != null)
        {
            int remaining = 4 - options.Count;
            if (remaining > 0) options.AddRange(q.wrongAnswers.Take(remaining));
        }
        
        options = options.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledBtns = answerButtons.OrderBy(x => Random.value).ToList();
    
        Sprite overridePressed = null;
        if (q.buttonStyleOverride != null && q.buttonStyleOverride.useOverride)
            overridePressed = LoadSpriteFromStreamingAssets(q.buttonStyleOverride.pressedImageName);
        
        // 스타일 적용을 먼저 수행
        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = shuffledBtns[i];
            Button btn = btnObj.GetComponent<Button>();
            
            btn.interactable = true;
            btn.onClick.RemoveAllListeners();

            if (i < options.Count)
            {
                string text = options[i];
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp) tmp.text = text;

                // 스타일 오버라이드 적용 또는 기본 상태 복구
                if (q.buttonStyleOverride != null && q.buttonStyleOverride.useOverride)
                {
                    ApplyButtonOverride(btnObj, q.buttonStyleOverride);
                }
                else
                {
                    // 기본값 복구 (위치가 0,0으로 리셋될 수 있음)
                    RevertToDefaultButtonStyle(btnObj);
                }

                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj, overridePressed));
                btnObj.SetActive(true);
            }
            else btnObj.SetActive(false);
        }

        // 위치 배치를 마지막에 수행하여 스타일 적용 시 리셋된 위치를 덮어씀
        List<GameObject> activeBtns = shuffledBtns.Take(options.Count).ToList();
        int half = Mathf.CeilToInt(activeBtns.Count / 2f);
        PlaceButtonsInArea(activeBtns.Take(half).ToList(), leftAreaRect);
        PlaceButtonsInArea(activeBtns.Skip(half).ToList(), rightAreaRect);
    }
    
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
            if (pressedSprite && btnObj.TryGetComponent(out Image img)) img.sprite = pressedSprite;
            btnObj.GetComponent<Button>().interactable = false;
            
            if (isLevelClear) HandleCorrectAnswer();
        }
        else HandleWrongAnswer();
    }
    
    /// <summary> 버튼 스타일 오버라이드 적용. </summary>
    private void ApplyButtonOverride(GameObject btnObj, ButtonOverrideSetting set)
    {
        if (btnObj == null) return;

        // 1. 비디오 관련 컴포넌트 정리 & Image 컴포넌트 확보
        if (btnObj.TryGetComponent(out UIVideoPlayer videoPlayer)) Destroy(videoPlayer);
        if (btnObj.TryGetComponent(out VideoPlayer vp)) Destroy(vp);
        if (btnObj.TryGetComponent(out RawImage rawImage)) Destroy(rawImage);

        Image btnImage = btnObj.GetComponent<Image>();
        if (btnImage == null) btnImage = btnObj.AddComponent<Image>();
        btnImage.enabled = true;

        Button btn = btnObj.GetComponent<Button>();
        if (btn) btn.targetGraphic = btnImage;

        // 2. 이미지 및 색상 적용
        Sprite normal = LoadSpriteFromStreamingAssets(set.normalImageName);
        if(normal) 
        { 
            btnImage.sprite = normal; 
            btnImage.color = set.buttonColor; 
            btnImage.type = Image.Type.Simple;
        }
        
        // 3. 크기(Size) 적용
        if (set.overrideSize != Vector2.zero)
        {
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            if (rt) rt.sizeDelta = set.overrideSize;
        }

        // 4. 그라데이션 끄기 및 눌림 상태 설정
        ImageGlobalGradient gradient = btnObj.GetComponent<ImageGlobalGradient>();
        if (gradient) gradient.enabled = false;
        
        if (btn && !string.IsNullOrEmpty(set.pressedImageName))
        {
            Sprite pressed = LoadSpriteFromStreamingAssets(set.pressedImageName);
            if (pressed)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                SpriteState newState = new SpriteState 
                { 
                    pressedSprite = pressed, 
                    highlightedSprite = normal, 
                    selectedSprite = normal, 
                    disabledSprite = pressed 
                };
                btn.spriteState = newState;
            }
        }
    }

    private void RevertToDefaultButtonStyle(GameObject btnObj)
    {
        if (JsonLoader.Instance == null || JsonLoader.Instance.settings == null) return;
        
        var globalSettings = JsonLoader.Instance.settings;
        if (globalSettings.questionButtons != null && globalSettings.questionButtons.Length > 0)
        {
            int selectedLevel = LevelSelectContext.SelectedLevel > 0 ? LevelSelectContext.SelectedLevel : 1;
            int levelIndex = Mathf.Clamp(selectedLevel - 1, 0, globalSettings.questionButtons.Length - 1);
            
            ButtonSetting defaultSetting = globalSettings.questionButtons[levelIndex];
            
            ButtonSetting bgOnlySetting = new ButtonSetting 
            {
                name = defaultSetting.name,
                position = defaultSetting.position,
                size = defaultSetting.size,
                rotation = defaultSetting.rotation,
                scale = defaultSetting.scale,
                buttonBackgroundImage = defaultSetting.buttonBackgroundImage,
                buttonSound = defaultSetting.buttonSound,
                buttonText = null 
            };
            
            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetButtonObj(btnObj, bgOnlySetting).Forget();
            }
        }
    }
}