using UnityEngine;
using UnityEngine.UI;

/// <summary> 화면 특정 위치를 연타하여 게임을 강제 종료하는 클래스. </summary>
public class GameCloser : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform rectTransform; // 터치 영역 UI

    private CloseSetting closeSetting; // 종료 설정 데이터 (JSON)

    // 내부 상태 변수
    private int clickCount = 0;     // 현재 클릭 횟수
    private float timer = 0f;       // 시간 측정용 타이머
    private bool counting = false;  // 카운트 진행 여부

    /// <summary> 초기화: JSON 설정 로드 및 터치 영역 배치. </summary>
    private void Start()
    {
        // 1. 설정 불러오기
        if (JsonLoader.Instance != null && JsonLoader.Instance.settings != null)
        {
            closeSetting = JsonLoader.Instance.settings.closeSetting;
        }

        if (closeSetting == null)
        {
            Debug.LogWarning("[GameCloser] CloseSetting is null. Script disabled.");
            this.enabled = false;
            return;
        }

        // 2. 컴포넌트 캐싱
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    
        // 3. UI 위치 및 투명도 설정
        if (rectTransform != null)
        {
            Vector2 anchor = closeSetting.position;
            
            // 앵커를 설정하여 위치 고정
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = anchor;
            rectTransform.anchoredPosition = Vector2.zero; // 앵커 기준 0,0

            // 이미지 색상(알파값) 적용
            if (rectTransform.TryGetComponent(out Image image))
            {
                image.color = new Color(1, 1, 1, closeSetting.imageAlpha);
            }
        }
    }

    /// <summary>
    /// 매 프레임 타이머 체크.
    /// 일정 시간 내에 연타하지 않으면 횟수 초기화.
    /// </summary>
    private void Update()
    {
        if (!counting) return;

        timer += Time.deltaTime;

        // 제한 시간을 초과하면 초기화
        if (timer >= closeSetting.resetClickTime)
        {
            ResetClickCount();
        }
    }

    /// <summary>
    /// 터치(클릭) 시 호출되는 메서드. 
    /// 클릭 횟수를 증가시키고, 목표치에 도달하면 게임 종료.
    /// </summary>
    public void Click()
    {
        counting = true;
        clickCount++;

        // 목표 횟수 도달 체크
        if (clickCount >= closeSetting.numToClose)
        {
            Debug.Log("[GameCloser] Force Exit Triggered!");
            GameManager.Instance?.ExitGame();
            ResetClickCount();
        }
    }

    /// <summary> 클릭 횟수 및 타이머 초기화. </summary>
    private void ResetClickCount()
    {
        clickCount = 0;
        timer = 0f;
        counting = false;
    }
}