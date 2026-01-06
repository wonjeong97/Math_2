using System;
using UnityEngine;

// 공통 UI 설정을 위한 인터페이스 정의
public interface IGameCommonSetting
{
    ButtonSetting backButton { get; }
    float buttonMargin { get; }
    ImageSetting correctImage { get; }
    ImageSetting wrongImage { get; }
    ButtonSetting retryButton { get; }
    ButtonSetting gameEndButton { get; }
    ImageSetting[] levelImages { get; }
    ImageSetting[] gameTypeImages { get; }
    LevelProgressSetting[] levelProgresses { get; }
}

public enum QuestionType
{
    SingleChoice,
    MultipleChoice,
    Sequence
}

[Serializable]
public class LevelProgressSetting
{
    public ImageSetting[] steps;
}

// -------------------- Guess Number --------------------
[Serializable]
public class GuessNumberQuestion
{
    public int level;
    public QuestionType type;
    public string questionText;
    public ImageSetting questionImage;
    public string[] correctAnswers;
    public string[] wrongAnswers;
}

[Serializable]
public class GuessNumberSetting : IGameCommonSetting // 인터페이스 구현
{   
    public ImageSetting backgroundImage;
    public GuessNumberQuestion[] questions;
    
    // IGameCommonSetting 구현 필드들
    public ImageSetting[] levelImages;
    public ImageSetting[] gameTypeImages;
    public LevelProgressSetting[] levelProgresses; 
    public ButtonSetting backButton;
    public float buttonMargin = 20f;
    public ImageSetting correctImage;
    public ImageSetting wrongImage;
    public ButtonSetting retryButton;
    public ButtonSetting gameEndButton;
    
    // 인터페이스 프로퍼티 구현
    ImageSetting[] IGameCommonSetting.levelImages => levelImages;
    ImageSetting[] IGameCommonSetting.gameTypeImages => gameTypeImages;
    LevelProgressSetting[] IGameCommonSetting.levelProgresses => levelProgresses;
    ButtonSetting IGameCommonSetting.backButton => backButton;
    float IGameCommonSetting.buttonMargin => buttonMargin;
    ImageSetting IGameCommonSetting.correctImage => correctImage;
    ImageSetting IGameCommonSetting.wrongImage => wrongImage;
    ButtonSetting IGameCommonSetting.retryButton => retryButton;
    ButtonSetting IGameCommonSetting.gameEndButton => gameEndButton;
}

// -------------------- Calculate Number --------------------
[Serializable]
public class CalculateNumberQuestion
{
    public int level;
    public QuestionType type;
    public string questionText;
    public string[] correctAnswers;
    public string[] wrongAnswers;
    public ImageSetting[] questionImages;
    public ButtonOverrideSetting buttonStyleOverride;
}

[Serializable]
public class ButtonOverrideSetting
{
    public bool useOverride;
    public string normalImageName;
    public string pressedImageName;
    public Color buttonColor = Color.white;
    public Vector2 overrideSize;
}

[Serializable]
public class CalculateNumberSetting : IGameCommonSetting // 인터페이스 구현
{   
    public ImageSetting backgroundImage;
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

    // 인터페이스 구현
    ImageSetting[] IGameCommonSetting.levelImages => levelImages;
    ImageSetting[] IGameCommonSetting.gameTypeImages => gameTypeImages;
    LevelProgressSetting[] IGameCommonSetting.levelProgresses => levelProgresses;
    ButtonSetting IGameCommonSetting.backButton => backButton;
    float IGameCommonSetting.buttonMargin => buttonMargin;
    ImageSetting IGameCommonSetting.correctImage => correctImage;
    ImageSetting IGameCommonSetting.wrongImage => wrongImage;
    ButtonSetting IGameCommonSetting.retryButton => retryButton;
    ButtonSetting IGameCommonSetting.gameEndButton => gameEndButton;
}

// -------------------- Number System --------------------
[Serializable]
public class NumberSystemQuestion
{
    public int level;
    public QuestionType type;
    public string questionText;
    public string[] correctAnswers;
    public string[] wrongAnswers;
    public ImageSetting questionImage; 
    public AnswerImagePair[] answerImages;
}

[Serializable]
public class AnswerImagePair
{
    public string answerText;
    public string imagePath;
    public Vector2 size;
}

[Serializable]
public class NumberSystemSetting : IGameCommonSetting // 인터페이스 구현
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

    // 인터페이스 구현
    ImageSetting[] IGameCommonSetting.levelImages => levelImages;
    ImageSetting[] IGameCommonSetting.gameTypeImages => gameTypeImages;
    LevelProgressSetting[] IGameCommonSetting.levelProgresses => levelProgresses;
    ButtonSetting IGameCommonSetting.backButton => backButton;
    float IGameCommonSetting.buttonMargin => buttonMargin;
    ImageSetting IGameCommonSetting.correctImage => correctImage;
    ImageSetting IGameCommonSetting.wrongImage => wrongImage;
    ButtonSetting IGameCommonSetting.retryButton => retryButton;
    ButtonSetting IGameCommonSetting.gameEndButton => gameEndButton;
}