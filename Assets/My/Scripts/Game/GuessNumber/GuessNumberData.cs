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
    public string[] correctAnswers; // 정답 목록 (순서 나열인 경우 순서대로 입력)
    public string[] wrongAnswers;   // 오답 후보군
}
