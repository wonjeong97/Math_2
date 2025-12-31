using UnityEngine;
using TMPro;

public class ButtonSpinner : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float rotateSpeed = 50f; // 회전 속도 (음수면 시계 방향)
    [SerializeField] private bool isSpinning = true;  // 회전 활성화 여부

    private RectTransform _textRect;
    private Quaternion _fixedRotation;

    /// <summary>
    /// 자식에 있는 TextMeshProUGUI의 RectTransform을 찾아 저장하고 그 객체의 현재 월드 회전값을 고정값으로 기록합니다.
    /// </summary>
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

    /// <summary>
    /// isSpinning이 true일 때 버튼 이미지(GameObject)를 Z축을 중심으로 회전시킵니다.
    /// </summary>
    /// <remarks>
    /// 회전 속도는 rotateSpeed에 비례하고 Time.deltaTime을 곱해 프레임 독립적으로 적용됩니다.
    /// rotateSpeed가 음수이면 시계 방향으로 회전합니다. isSpinning이 false이면 동작을 수행하지 않습니다.
    /// </remarks>
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

    /// <summary>
    /// 버튼의 회전 동작을 외부에서 켜거나 끕니다.
    /// </summary>
    /// <param name="active">회전을 활성화하려면 true, 비활성화하려면 false.</param>
    public void SetSpinning(bool active)
    {
        isSpinning = active;
        // 멈출 때 각도를 0으로 깔끔하게 리셋하고 싶다면 아래 주석 해제
        // if (!active) transform.rotation = Quaternion.identity;
    }
}