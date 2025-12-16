using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UI Image 컴포넌트에 4방향(Top-Left, Top-Right, Bottom-Left, Bottom-Right) 그라데이션 효과를 적용하는 스크립트.
/// BaseMeshEffect를 상속받아 UI 메쉬 생성 단계에서 정점 색상을 직접 수정.
/// </summary>
[RequireComponent(typeof(Image))]
public class ImageGlobalGradient : BaseMeshEffect
{
    // 그라데이션 4개 모서리 색상
    public Color topLeft = Color.white;
    public Color topRight = Color.white;
    public Color bottomLeft = Color.white;
    public Color bottomRight = Color.white;

    /// <summary> 외부에서 그라데이션 색상을 설정하고 UI 갱신을 요청. </summary>
    public void SetGradient(Color tl, Color tr, Color bl, Color br)
    {
        topLeft = tl;
        topRight = tr;
        bottomLeft = bl;
        bottomRight = br;
        
        // UI 메쉬 재생성 요청 (ModifyMesh 호출 유도)
        if (graphic != null) graphic.SetVerticesDirty();
    }

    /// <summary>
    /// UI 메쉬가 생성될 때 호출되는 오버라이드 메서드.
    /// 정점(Vertex) 정보를 가져와 색상을 보간(Lerp)하여 적용.
    /// </summary>
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        List<UIVertex> vertexList = new List<UIVertex>();
        vh.GetUIVertexStream(vertexList);

        int count = vertexList.Count;
        if (count == 0) return;

        // 1. 이미지의 전체 범위(Bounds) 계산
        float minX = vertexList[0].position.x;
        float maxX = vertexList[0].position.x;
        float minY = vertexList[0].position.y;
        float maxY = vertexList[0].position.y;

        for (int i = 1; i < count; i++)
        {
            float x = vertexList[i].position.x;
            float y = vertexList[i].position.y;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        float width = maxX - minX;
        float height = maxY - minY;

        // 2. 각 정점마다 위치 비율에 따라 색상 혼합(Lerp)
        for (int i = 0; i < count; i++)
        {
            UIVertex uiVertex = vertexList[i];
            
            // 0 ~ 1 사이 정규화 좌표 (Width/Height가 0일 경우 예외처리)
            float normalizedX = (width == 0) ? 0 : (uiVertex.position.x - minX) / width;
            float normalizedY = (height == 0) ? 0 : (uiVertex.position.y - minY) / height;

            // 상단/하단 가로축 색상 보간
            Color colorTop = Color.Lerp(topLeft, topRight, normalizedX);
            Color colorBottom = Color.Lerp(bottomLeft, bottomRight, normalizedX);

            // 수직축 보간하여 최종 색상 결정
            uiVertex.color = Color.Lerp(colorBottom, colorTop, normalizedY);
            
            vertexList[i] = uiVertex;
        }

        // 3. 수정된 정점 데이터 적용
        vh.Clear();
        vh.AddUIVertexTriangleStream(vertexList);
    }
}