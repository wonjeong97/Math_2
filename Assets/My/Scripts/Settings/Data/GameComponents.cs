using System;
using UnityEngine;

[Serializable]
public class ButtonSetting
{
    public string name;
    public Vector2 position;
    public Vector2 size;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;
    public ImageSetting buttonBackgroundImage;
    public TextSetting buttonText;
    public string buttonSound;
}

[Serializable]
public class SoundSetting
{
    public string key;
    public string clipPath;
    public float volume = 1.0f;
}

[Serializable]
public class VideoSetting
{
    public string name;
    public Vector2 position;
    public Vector2 size;
    public string fileName;
    public float volume;
}

[Serializable]
public class FontMaps
{
    public string font1;
    public string font2;
    public string font3;
    public string font4;
    public string font5;
}

[Serializable]
public class CloseSetting
{
    public Vector2 position;
    public int numToClose;
    public float resetClickTime;
    public float imageAlpha;
}