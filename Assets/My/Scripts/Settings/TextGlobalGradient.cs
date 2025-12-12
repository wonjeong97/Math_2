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

    private void OnEnable()
    {
        // 켜질 때 적용
        ApplyGradient();
    }

    // AutoSize는 Update 루프에서 계산되므로, 모든 계산이 끝난 LateUpdate에서 색상을 입혀야 함
    private void LateUpdate()
    {
        // 성능 최적화: 텍스트가 변경되었거나 활성화 상태일 때만 체크하도록 할 수 있으나,
        // AutoSize의 경우 프레임마다 미세하게 변할 수 있어 LateUpdate에서 지속 적용하는 것이 가장 안전함.
        ApplyGradient();
    }

    /// <summary> 외부에서 색상을 설정하고 즉시 적용 </summary>
    public void SetGradient(Color tl, Color tr, Color bl, Color br)
    {
        topLeft = tl;
        topRight = tr;
        bottomLeft = bl;
        bottomRight = br;
        // LateUpdate에서 자동 적용되므로 여기서 호출 안 해도 되지만, 즉각 반응을 위해 남겨둠
        ApplyGradient();
    }

    /// <summary> 텍스트 전체 영역 기준 그라데이션 적용 </summary>
    public void ApplyGradient()
    {
        if (!_tmp) _tmp = GetComponent<TextMeshProUGUI>();

        // [중요 수정] ForceMeshUpdate 제거
        // _tmp.ForceMeshUpdate(); -> 이 호출이 AutoSize 계산을 방해하거나 초기화시킴
        // LateUpdate에서 호출하므로 이미 최신 메쉬 정보가 있음

        TMP_TextInfo textInfo = _tmp.textInfo;
        int charCount = textInfo.characterCount;
        
        // 텍스트가 없거나 메쉬 정보가 없으면 리턴
        if (charCount == 0 || textInfo.meshInfo == null) return;

        // 2. 전체 텍스트의 Bounds(범위) 계산
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int i = 0; i < charCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;
            
            // AutoSize가 적용된 후의 실제 정점 위치를 가져옴
            int matIndex = textInfo.characterInfo[i].materialReferenceIndex;
            int vertIndex = textInfo.characterInfo[i].vertexIndex;
            
            // meshInfo 배열 범위 체크 (안전장치)
            if (matIndex >= textInfo.meshInfo.Length) continue;

            Vector3[] vertices = textInfo.meshInfo[matIndex].vertices;
            if (vertices == null) continue;

            // 글자의 4개 꼭짓점 확인
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

        // 3. 색상 보간 및 적용
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
                
                // 위치 비율 (0~1)
                float hRatio = (width == 0) ? 0 : (pos.x - minX) / width;
                float vRatio = (height == 0) ? 0 : (pos.y - minY) / height;

                Color bottom = Color.Lerp(bottomLeft, bottomRight, hRatio);
                Color top = Color.Lerp(topLeft, topRight, hRatio);
                
                newColors[vertIndex + v] = Color.Lerp(bottom, top, vRatio);
            }
        }

        // 4. 메쉬에 색상 업데이트
        _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}