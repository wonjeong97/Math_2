using System.Collections.Generic;
using UnityEngine;

/// <summary> UI 오브젝트 배치와 관련된 편의 기능을 제공하는 유틸리티 클래스. </summary>
public static class UILayoutUtility
{
    /// <summary>
    /// 지정된 영역(areaRect) 내에 오브젝트들을 그리드 형태로 나누고, 
    /// 각 셀 내부에서 랜덤한 위치(Jitter)를 적용하여 배치함.
    /// </summary>
    public static void PlaceObjectsRandomlyInGrid(List<GameObject> objectsToPlace, RectTransform areaRect, float margin = 20f, int rows = 2, int columns = 1)
    {
        // 입력 유효성 검사
        if (!areaRect || objectsToPlace == null || objectsToPlace.Count == 0 || rows <= 0 || columns <= 0) return;

        Rect rect = areaRect.rect;
        Vector2 halfAreaSize = rect.size * 0.5f;

        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;

        // 1. 슬롯(격자) 중심 좌표 생성
        List<Vector2> slots = new List<Vector2>();
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                float posX = -halfAreaSize.x + cellWidth * (col + 0.5f);
                float posY = halfAreaSize.y - cellHeight * (row + 0.5f);
                slots.Add(new Vector2(posX, posY));
            }
        }

        // 2. 슬롯 랜덤 섞기
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        // 3. 오브젝트 배치
        int count = Mathf.Min(objectsToPlace.Count, slots.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject obj = objectsToPlace[i];
            if (obj == null) continue;

            RectTransform rt = obj.GetComponent<RectTransform>();
            if (rt == null) continue;
            
            Vector3 scale = rt.localScale;
            float w = rt.sizeDelta.x * scale.x;
            float h = rt.sizeDelta.y * scale.y;

            // 여유 공간(Jitter) 계산
            float jitterX = Mathf.Max(0f, (cellWidth - w) * 0.5f - margin);
            float jitterY = Mathf.Max(0f, (cellHeight - h) * 0.5f - margin);

            // 슬롯 중심에서 랜덤하게 약간 이동
            Vector2 basePos = slots[i];
            float offsetX = jitterX > 0 ? Random.Range(-jitterX, jitterX) : 0f;
            float offsetY = jitterY > 0 ? Random.Range(-jitterY, jitterY) : 0f;

            // 최종 적용
            rt.SetParent(areaRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = basePos + new Vector2(offsetX, offsetY);
        }
    }
}