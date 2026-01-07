using UnityEngine;
using TMPro;

/// <summary> TextMeshProUGUI 전체 영역에 4방향 그라데이션을 적용함. </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextGlobalGradient : MonoBehaviour
{
    public Color topLeft = Color.white;
    public Color topRight = Color.white;
    public Color bottomLeft = Color.white;
    public Color bottomRight = Color.white;

    private TextMeshProUGUI _tmp;

    private void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        ApplyGradient();
    }

    private void LateUpdate()
    {
        ApplyGradient();
    }

    /// <summary> 색상을 설정하고 즉시 적용함. </summary>
    public void SetGradient(Color tl, Color tr, Color bl, Color br)
    {
        topLeft = tl;
        topRight = tr;
        bottomLeft = bl;
        bottomRight = br;
        
        ApplyGradient();
    }

    /// <summary> 전체 텍스트 영역을 계산하여 그라데이션을 적용함. </summary>
    public void ApplyGradient()
    {
        if (!_tmp) _tmp = GetComponent<TextMeshProUGUI>();

        TMP_TextInfo textInfo = _tmp.textInfo;
        int charCount = textInfo.characterCount;
        
        if (charCount == 0 || textInfo.meshInfo == null) return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < charCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;
            
            int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
            int vertIndex = textInfo.characterInfo[i].vertexIndex;
            
            if (matIndex >= textInfo.meshInfo.Length) continue;

            Vector3[] vertices = textInfo.meshInfo[matIndex].vertices;
            if (vertices == null) continue;

            for (int v = 0; v < 4; v++)
            {
                Vector3 pos = vertices[vertIndex + v];
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }
        }

        if (minX >= maxX || minY >= maxY) return;

        float width = maxX - minX;
        float height = maxY - minY;

        for (int i = 0; i < charCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;

            int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
            int vertIndex = textInfo.characterInfo[i].vertexIndex;
            
            if (matIndex >= textInfo.meshInfo.Length) continue;

            Color32[] newColors = textInfo.meshInfo[matIndex].colors32;
            Vector3[] vertices = textInfo.meshInfo[matIndex].vertices;
            
            if (newColors == null || vertices == null) continue;

            for (int v = 0; v < 4; v++)
            {
                Vector3 pos = vertices[vertIndex + v];
                
                float hRatio = (width == 0) ? 0 : (pos.x - minX) / width;
                float vRatio = (height == 0) ? 0 : (pos.y - minY) / height;

                Color bottom = Color.Lerp(bottomLeft, bottomRight, hRatio);
                Color top = Color.Lerp(topLeft, topRight, hRatio);
                
                newColors[vertIndex + v] = Color.Lerp(bottom, top, vRatio);
            }
        }

        _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}