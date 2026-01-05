using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary> VideoPlayer가 사용하는 RawImage에 그라데이션을 적용하는 스크립트. </summary>
[RequireComponent(typeof(RawImage))]
public class RawImageGlobalGradient : BaseMeshEffect
{
    public Color topLeft = Color.white;
    public Color topRight = Color.white;
    public Color bottomLeft = Color.white;
    public Color bottomRight = Color.white;

    public void SetGradient(Color tl, Color tr, Color bl, Color br)
    {
        topLeft = tl;
        topRight = tr;
        bottomLeft = bl;
        bottomRight = br;
        
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        List<UIVertex> vertexList = new List<UIVertex>();
        vh.GetUIVertexStream(vertexList);

        int count = vertexList.Count;
        if (count == 0) return;

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

        for (int i = 0; i < count; i++)
        {
            UIVertex uiVertex = vertexList[i];
            
            float normalizedX = (width == 0) ? 0 : (uiVertex.position.x - minX) / width;
            float normalizedY = (height == 0) ? 0 : (uiVertex.position.y - minY) / height;

            Color colorTop = Color.Lerp(topLeft, topRight, normalizedX);
            Color colorBottom = Color.Lerp(bottomLeft, bottomRight, normalizedX);

            // 기존 색상(비디오)에 그라데이션 색상을 곱해서 틴트 효과 적용
            Color gradientColor = Color.Lerp(colorBottom, colorTop, normalizedY);
            uiVertex.color = uiVertex.color * gradientColor;
            
            vertexList[i] = uiVertex;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(vertexList);
    }
}