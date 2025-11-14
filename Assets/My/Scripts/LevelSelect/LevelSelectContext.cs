public enum GameType
{
    GuessNumber,     // 수 맞추기
    CalculateNumber, // 수 계산하기
    NumberSystem     // 수의 체계
}

public static class LevelSelectContext
{
    public static int SelectedLevel { get; set; }      // 1~5
    public static GameType SelectedGameType { get; set; }
}