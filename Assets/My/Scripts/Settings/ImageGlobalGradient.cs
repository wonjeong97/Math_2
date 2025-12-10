using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Image))]
public class ImageGlobalGradient : BaseMeshEffect
{
    public Color topLeft = Color.white;
    public Color topRight = Color.white;
    public Color bottomLeft = Color.white;
    public Color bottomRight = Color.white;

    // 매개변수를 string에서 Color로 변경하여 직접 할당
    public void SetGradient(Color tl, Color tr, Color bl, Color br)
    {
        topLeft = tl;
        topRight = tr;
        bottomLeft = bl;
        bottomRight = br;
        
        // UI 갱신 요청
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        List<UIVertex> vertexList = new List<UIVertex>();
        vh.GetUIVertexStream(vertexList);

        int count = vertexList.Count;
        if (count == 0) return;

        // 이미지의 최소/최대 좌표(Bounds) 계산
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

        // 각 정점마다 위치 비율에 따라 색상 혼합(Lerp)
        for (int i = 0; i < count; i++)
        {
            UIVertex uiVertex = vertexList[i];
            
            // 0 ~ 1 사이 정규화 좌표 (예외처리 포함)
            float normalizedX = (width == 0) ? 0 : (uiVertex.position.x - minX) / width;
            float normalizedY = (height == 0) ? 0 : (uiVertex.position.y - minY) / height;

            // 상단/하단 가로 보간
            Color colorTop = Color.Lerp(topLeft, topRight, normalizedX);
            Color colorBottom = Color.Lerp(bottomLeft, bottomRight, normalizedX);

            // 수직 보간하여 최종 색상 결정
            uiVertex.color = Color.Lerp(colorBottom, colorTop, normalizedY);
            
            vertexList[i] = uiVertex;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(vertexList);
    }
}