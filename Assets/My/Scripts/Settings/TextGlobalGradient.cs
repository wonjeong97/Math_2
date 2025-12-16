using UnityEngine;
using TMPro;

/// <summary>
/// TextMeshProUGUI의 전체 텍스트 영역에 대해 4방향(Top-Left, Top-Right, Bottom-Left, Bottom-Right) 그라데이션을 적용하는 클래스.
/// (기본 TMP 그라데이션은 글자 단위로 적용되지만, 이 스크립트는 전체 문장을 하나의 덩어리로 보고 색상을 입힘)
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextGlobalGradient : MonoBehaviour
{
    // 4방향 그라데이션 색상
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
        // 활성화 시 즉시 적용
        ApplyGradient();
    }

    /// <summary>
    /// 매 프레임 UI 레이아웃 계산 후(LateUpdate) 색상을 다시 입힘.
    /// (TMP의 AutoSize 기능이나 텍스트 변경 시 메쉬가 재생성되므로 지속적인 업데이트 필요)
    /// </summary>
    private void LateUpdate()
    {
        ApplyGradient();
    }

    /// <summary> 외부에서 색상을 설정하고 즉시 적용. </summary>
    public void SetGradient(Color tl, Color tr, Color bl, Color br)
    {
        topLeft = tl;
        topRight = tr;
        bottomLeft = bl;
        bottomRight = br;
        
        ApplyGradient();
    }

    /// <summary>
    /// 실제 그라데이션 로직 수행.
    /// 전체 텍스트의 Bounds를 계산하고, 각 정점의 위치 비율에 따라 색상을 보간.
    /// </summary>
    public void ApplyGradient()
    {
        if (!_tmp) _tmp = GetComponent<TextMeshProUGUI>();

        // [참고] ForceMeshUpdate()는 AutoSize 계산을 방해할 수 있으므로 제거함.
        // LateUpdate에서 호출되므로 이미 최신 메쉬 정보가 존재함.

        TMP_TextInfo textInfo = _tmp.textInfo;
        int charCount = textInfo.characterCount;
        
        // 텍스트가 없거나 메쉬 정보가 유효하지 않으면 리턴
        if (charCount == 0 || textInfo.meshInfo == null) return;

        // 1. 전체 텍스트의 실제 표시 영역(Bounds) 계산
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < charCount; i++)
        {
            // 보이지 않는 문자(공백 등)는 건너뜀
            if (!textInfo.characterInfo[i].isVisible) continue;
            
            int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
            int vertIndex = textInfo.characterInfo[i].vertexIndex;
            
            // 배열 범위 안전장치
            if (matIndex >= textInfo.meshInfo.Length) continue;

            Vector3[] vertices = textInfo.meshInfo[matIndex].vertices;
            if (vertices == null) continue;

            // 해당 글자의 4개 꼭짓점을 확인하여 전체 min/max 갱신
            for (int v = 0; v < 4; v++)
            {
                Vector3 pos = vertices[vertIndex + v];
                if (pos.x < minX) minX = pos.x;
                if (pos.x > maxX) maxX = pos.x;
                if (pos.y < minY) minY = pos.y;
                if (pos.y > maxY) maxY = pos.y;
            }
        }

        // 유효한 영역이 없으면 리턴
        if (minX >= maxX || minY >= maxY) return;

        float width = maxX - minX;
        float height = maxY - minY;

        // 2. 각 정점의 위치 비율에 따라 색상 보간(Lerp) 및 적용
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
                
                // 전체 영역 내에서의 가로/세로 비율 (0~1) 계산
                float hRatio = (width == 0) ? 0 : (pos.x - minX) / width;
                float vRatio = (height == 0) ? 0 : (pos.y - minY) / height;

                // 상단/하단 가로축 색상 보간
                Color bottom = Color.Lerp(bottomLeft, bottomRight, hRatio);
                Color top = Color.Lerp(topLeft, topRight, hRatio);
                
                // 수직축 보간하여 최종 색상 결정
                newColors[vertIndex + v] = Color.Lerp(bottom, top, vRatio);
            }
        }

        // 3. 수정된 색상 데이터를 메쉬에 반영
        _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}