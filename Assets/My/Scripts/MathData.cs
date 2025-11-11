using System;

[Serializable]
public class MathSetting
{
    public TextSetting root;

    public string question;     
    public int answerIndex;    
    public ButtonSetting answerButton;
}

[Serializable]
public class MathData
{
    public MathSetting[] maths;
    
    public static readonly string[] AnswerLabels =
    {
        "0",    // 0
        "1",    // 1
        "2",    // 2
        "3",    // 3
        "√2",   // 루트2
        "√4",   // 루트4
        "√6",   // 루트6
        "-2",   // -2
        "-4",   // -4
        "1/2",  // 1/2
        "1/3",  // 1/3
        "4/5",  // 4/5
        "0.2",  // 0.2
        "0.3",  // 0.3
        "0.5",  // 0.5
        "π"     // 파이
    };
}