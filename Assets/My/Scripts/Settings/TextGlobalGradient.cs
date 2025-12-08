using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TextGlobalGradient : MonoBehaviour
{
    // 그라데이션 색상 저장
    public Color topLeft = Color.white;
    public Color topRight = Color.white;
    public Color bottomLeft = Color.white;
    public Color bottomRight = Color.white;

    private TextMeshProUGUI _tmp;

    private void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
    }

    // 오브젝트가 활성화될 때마다 실행
    private void OnEnable()
    {
        ApplyGradient();
    }

    /// <summary> 외부에서 색상을 설정하고 즉시 적용 </summary>
    public void SetGradient(Color tl, Color tr, Color bl, Color br)
    {
        topLeft = tl;
        topRight = tr;
        bottomLeft = bl;
        bottomRight = br;
        ApplyGradient();
    }

    /// <summary> 텍스트 전체 영역 기준 그라데이션 적용 </summary>
    public void ApplyGradient()
    {
        if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();

        // 1. 최신 텍스트 정보 갱신
        _tmp.ForceMeshUpdate(); 

        TMP_TextInfo textInfo = _tmp.textInfo;
        int charCount = textInfo.characterCount;
        if (charCount == 0) return;

        // 2. 전체 텍스트의 Bounds(범위) 계산
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < charCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;
            
            var bl = textInfo.characterInfo[i].bottomLeft;
            var tr = textInfo.characterInfo[i].topRight;

            if (bl.x < minX) minX = bl.x;
            if (tr.x > maxX) maxX = tr.x;
            if (bl.y < minY) minY = bl.y;
            if (tr.y > maxY) maxY = tr.y;
        }

        if (minX >= maxX || minY >= maxY) return;

        float width = maxX - minX;
        float height = maxY - minY;

        // 3. 색상 보간 및 적용
        for (int i = 0; i < charCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;

            int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
            int vertIndex = textInfo.characterInfo[i].vertexIndex;

            Color32[] newColors = textInfo.meshInfo[matIndex].colors32;
            Vector3[] vertices = textInfo.meshInfo[matIndex].vertices;

            for (int v = 0; v < 4; v++)
            {
                Vector3 pos = vertices[vertIndex + v];
                
                // 위치 비율 (0~1)
                float hRatio = (pos.x - minX) / width;
                float vRatio = (pos.y - minY) / height;

                Color bottom = Color.Lerp(bottomLeft, bottomRight, hRatio);
                Color top = Color.Lerp(topLeft, topRight, hRatio);
                
                newColors[vertIndex + v] = Color.Lerp(bottom, top, vRatio);
            }
        }

        // 4. 메쉬에 색상 업데이트
        _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}