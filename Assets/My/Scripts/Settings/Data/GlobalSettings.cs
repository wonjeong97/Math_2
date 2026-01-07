using System;

[Serializable]
public class Settings
{   
    public float inactivityTime; // 입력 대기 시간
    public float fadeTime;       // 페이드 효과 시간
    
    public CloseSetting closeSetting;
    public FontMaps fontMap;
    public SoundSetting[] sounds;
    
    // 공통 사용 UI
    public ButtonSetting[] questionButtons;
    public TextSetting gameQuestionText;
}