using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;

/// <summary> '수의 체계(NumberSystem)' 게임을 관리함. </summary>
public class NumberSystemManager : BaseGameManager<NumberSystemSetting, NumberSystemQuestion>
{
    [Header("--- NumberSystem Specific ---")]
    [SerializeField] private GameObject backgroundObj;
    [SerializeField] private Transform leftQuestionZone;    
    [SerializeField] private Transform rightQuestionZone;   

    private int _currentSequenceIndex;          
    private int _foundAnswerCount;              
    private HashSet<string> _foundAnswersSet;   

    protected override string JsonPath => GameConstants.Path.JsonNumberSystem;
    protected override int GetQuestionLevel(NumberSystemQuestion q) => q.level;
    
    protected override void OnSetupChildComponents()
    {
        if (backgroundObj != null && managerSetting != null && managerSetting.backgroundImage != null && UIManager.Instance != null)
        {
            UIManager.Instance.SetImageObj(backgroundObj, managerSetting.backgroundImage, this.GetCancellationTokenOnDestroy()).Forget();
        }
        else if (UIManager.Instance == null)
        {
            Debug.LogError("[NumberSystemManager] UIManager.Instance is null");
        }
    }

    protected override void StartGameLogic()
    {   
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(GameConstants.Sound.NumberSystemBGM);
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

    protected override void SetupSpecificQuestionUI(NumberSystemQuestion q)
    {
        _currentSequenceIndex = 0;
        _foundAnswerCount = 0;
        _foundAnswersSet = new HashSet<string>();

        UpdateProgressImage(q.level, currentQuestionIndex);

        bool isTextLeft = Random.Range(0, 2) == 0;
        Transform textParent = isTextLeft ? leftQuestionZone : rightQuestionZone;
        Transform contentParent = isTextLeft ? rightQuestionZone : leftQuestionZone;

        if (questionTextObj && textParent)
        {
            questionTextObj.transform.SetParent(textParent, false);
            bool hasText = !string.IsNullOrEmpty(q.questionText);
            questionTextObj.text = hasText ? q.questionText : "";
            questionTextObj.gameObject.SetActive(hasText);
        }

        bool hasImage = q.questionImage != null && !string.IsNullOrEmpty(q.questionImage.sourceImage);
        if (hasImage && questionImageObj)
        {
            questionImageObj.transform.SetParent(contentParent, false);
            UIManager.Instance.SetImageObj(questionImageObj.gameObject, q.questionImage).Forget();
            questionImageObj.gameObject.SetActive(true);
        }
        else
        {
            if (questionImageObj) questionImageObj.gameObject.SetActive(false);
        }
    }

    /// <summary> 정답 버튼을 설정하고 배치함. </summary>
    protected override void SetupAnswerButtons(NumberSystemQuestion q)
    {
        List<string> options = new List<string>();
        if (q.correctAnswers != null) options.AddRange(q.correctAnswers);
        if (q.wrongAnswers != null)
        {
            int remaining = 4 - options.Count;
            if (remaining > 0) options.AddRange(q.wrongAnswers.Take(remaining));
        }

        options = options.OrderBy(x => Random.value).ToList();
        List<GameObject> shuffledButtons = answerButtons.OrderBy(x => Random.value).ToList();

        // 1. 버튼 설정
        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = shuffledButtons[i];
            Button btn = btnObj.GetComponent<Button>();
            
            btn.interactable = true;
            btn.onClick.RemoveAllListeners();

            if (i < options.Count)
            {
                // 비디오 플레이어 작동을 위해 버튼 활성화를 먼저 수행
                btnObj.SetActive(true);

                string text = options[i];
                TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                
                // 텍스트에 매핑된 이미지 정보 확인
                AnswerImagePair pair = GetAnswerImagePair(q, text);
                
                if (pair != null && !string.IsNullOrEmpty(pair.imagePath))
                {
                    // [이미지 모드]
                    if (tmp) tmp.text = "";

                    // UIManager를 통해 안전하게 이미지 모드로 전환
                    if (UIManager.Instance != null)
                    {
                        Image btnImage = UIManager.Instance.SwitchToImageMode(btnObj);
                        
                        if (btnImage != null)
                        {
                            Sprite s = LoadSpriteFromStreamingAssets(pair.imagePath);
                            if (s)
                            {
                                btnImage.sprite = s;
                                btnImage.color = Color.white;
                                btnImage.type = Image.Type.Simple;
                                
                                var gradient = btnObj.GetComponent<ImageGlobalGradient>();
                                if(gradient) gradient.enabled = false;
                                
                                RectTransform btnRect = btnObj.GetComponent<RectTransform>();
                                if (pair.size != Vector2.zero && btnRect)
                                {
                                    float maxWidth = 900f; 
                                    Vector2 finalSize = pair.size;
                                    if (finalSize.x > maxWidth)
                                    {
                                        float ratio = maxWidth / finalSize.x;
                                        finalSize.x *= ratio;
                                        finalSize.y *= ratio;
                                    }
                                    btnRect.sizeDelta = finalSize;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // [텍스트 모드] -> 기본 스타일 복구
                    RevertToDefaultButtonStyle(btnObj);
                    
                    if (tmp) tmp.text = text;
                }

                btn.onClick.AddListener(() => OnAnswerClicked(text, btnObj));
            }
            else 
            {
                btnObj.SetActive(false);
            }
        }

        // 2. 버튼 배치
        List<GameObject> activeBtns = shuffledButtons.Take(options.Count).ToList();
        int half = Mathf.CeilToInt(activeBtns.Count / 2f);
        UILayoutUtility.PlaceObjectsRandomlyInGrid(activeBtns.Take(half).ToList(), leftAreaRect, managerSetting.buttonMargin);
        UILayoutUtility.PlaceObjectsRandomlyInGrid(activeBtns.Skip(half).ToList(), rightAreaRect, managerSetting.buttonMargin);
    }

    private void OnAnswerClicked(string clickedText, GameObject btnObj)
    {
        if (isProcessing) return;
        bool isCorrect = false;
        bool isLevelClear = false;
        
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(GameConstants.Sound.ButtonClick);    
        }

        switch (currentQuestion.type)
        {
            case QuestionType.SingleChoice:
                if (currentQuestion.correctAnswers.Contains(clickedText)) { isCorrect = true; isLevelClear = true; }
                break;
                
            case QuestionType.MultipleChoice:
                if (currentQuestion.correctAnswers.Contains(clickedText))
                {
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

    private AnswerImagePair GetAnswerImagePair(NumberSystemQuestion q, string text)
    {
        if (q.answerImages == null) return null;
        return q.answerImages.FirstOrDefault(x => x.answerText == text);
    }

    /// <summary> 버튼 스타일을 현재 레벨의 기본 설정으로 복구함. </summary>
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