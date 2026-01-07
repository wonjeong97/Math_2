using UnityEngine;
using TMPro;

/// <summary> 버튼을 회전시키되, 텍스트는 정방향을 유지하도록 함. </summary>
public class ButtonSpinner : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float rotateSpeed = 50f; 
    [SerializeField] private bool isSpinning = true; 

    private RectTransform _textRect;
    private Quaternion _fixedRotation;

    private void Awake()
    {
        var textComp = GetComponentInChildren<TextMeshProUGUI>();
        if (textComp != null)
        {
            _textRect = textComp.rectTransform;
            _fixedRotation = _textRect.rotation; 
        }
    }

    private void Update()
    {
        if (!isSpinning) return;
        transform.Rotate(0, 0, -rotateSpeed * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!isSpinning || _textRect == null) return;
        // 텍스트의 회전을 초기값으로 고정함
        _textRect.rotation = _fixedRotation;
    }

    /// <summary> 회전 동작을 켜거나 끔. </summary>
    public void SetSpinning(bool active)
    {
        isSpinning = active;
    }
}