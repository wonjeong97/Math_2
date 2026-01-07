using System;
using TMPro;
using UnityEngine;

[Serializable]
public enum UIImageType
{
    Simple = 0,
    Sliced,
    Tiled,
    Filled
}

[Serializable]
public class GradientData
{
    public Color topLeft;
    public Color topRight;
    public Color bottomLeft;
    public Color bottomRight;
}

[Serializable]
public class ImageSetting
{
    public string name;
    public Vector2 position;
    public Vector2 size;
    public Vector3 rotation;
    public Vector3 scale = Vector3.one;
    public string sourceImage;
    public Color color = Color.white;
    public UIImageType type = UIImageType.Simple;
    
    [Header("Fade Settings")]
    public bool useFade;           
    public float fadeDuration = 1f;
    public bool isFadeOut;         
    public bool loop;              
}

[Serializable]
public class TextSetting
{
    public string name;
    public Vector2 position;
    public Vector3 rotation;
    public string text;
    public string fontName;
    public float fontSize;
    public Color fontColor = Color.white;
    public TextAlignmentOptions alignment = TextAlignmentOptions.Center;
    
    public bool useGradient; 
    public bool useGlobalGradient;
    public Color gradientTopLeft = Color.white;
    public Color gradientTopRight = Color.white;
    public Color gradientBottomLeft = Color.white;
    public Color gradientBottomRight = Color.white;
}