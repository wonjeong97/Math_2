using UnityEngine;
using TMPro;

public class ButtonSpinner : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float rotateSpeed = 50f; // 회전 속도 (음수면 시계 방향)
    [SerializeField] private bool isSpinning = true;  // 회전 활성화 여부

    private RectTransform _textRect;
    private Quaternion _fixedRotation;

    private void Awake()
    {
        // 자식으로 있는 TextMeshProUGUI를 찾음
        var textComp = GetComponentInChildren<TextMeshProUGUI>();
        if (textComp != null)
        {
            _textRect = textComp.rectTransform;
            // 텍스트가 원래 가지고 있던 회전값(보통 0)을 기억
            _fixedRotation = _textRect.rotation; 
        }
    }

    private void Update()
    {
        if (!isSpinning) return;

        // 1. 버튼(이미지) 자체를 회전시킴
        transform.Rotate(0, 0, -rotateSpeed * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!isSpinning || _textRect == null) return;

        // 2. 텍스트의 회전값을 강제로 초기값(정방향)으로 고정
        // 부모가 회전했더라도 자식은 월드 기준으로 이 회전값을 유지함
        _textRect.rotation = _fixedRotation;
    }

    /// <summary> 외부에서 회전 여부를 제어할 수 있는 함수 </summary>
    public void SetSpinning(bool active)
    {
        isSpinning = active;
        // 멈출 때 각도를 0으로 깔끔하게 리셋하고 싶다면 아래 주석 해제
        // if (!active) transform.rotation = Quaternion.identity;
    }
}