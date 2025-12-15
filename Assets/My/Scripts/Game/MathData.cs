using System;
using UnityEngine;

public enum QuestionType
{
    SingleChoice,   // 정답 1개 (기존 방식)
    MultipleChoice, // 화면에 있는 정답 버튼을 모두 눌러야 함
    Sequence        // 정답을 순서대로 눌러야 함
}

[Serializable]
public class GuessNumberQuestion
{
    public int level;
    public QuestionType type;       // 문제 유형
    public string questionText;     // 문제 텍스트
    public ImageSetting questionImage;
    public string[] correctAnswers; // 정답 목록 (순서 나열인 경우 순서대로 입력)
    public string[] wrongAnswers;   // 오답 후보군
}

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

[Serializable]
public class NumberSystemQuestion
{
    public int level;
    public QuestionType type;
    public string questionText;
    
    public string[] correctAnswers;
    public string[] wrongAnswers;
    
    public ImageSetting questionImage; 
    public VideoSetting questionVideo; 
    
    public AnswerImagePair[] answerImages;
}

[Serializable]
public class AnswerImagePair
{
    public string answerText; // 매핑할 답변 텍스트 (correctAnswers/wrongAnswers에 있는 값)
    public string imagePath;  // StreamingAssets 내부 경로
    public Vector2 size;
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