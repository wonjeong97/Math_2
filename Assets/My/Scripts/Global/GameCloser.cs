using UnityEngine;
using UnityEngine.UI;

/// <summary> 화면 특정 위치를 연타하여 게임을 강제 종료함. </summary>
public class GameCloser : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform rectTransform; 

    private CloseSetting closeSetting; 

    private int clickCount = 0;     
    private float timer = 0f;       
    private bool counting = false;  

    private void Start()
    {
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

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    
        if (rectTransform != null)
        {
            Vector2 anchor = closeSetting.position;
            
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = anchor;
            rectTransform.anchoredPosition = Vector2.zero; 

            if (rectTransform.TryGetComponent(out Image image))
            {
                image.color = new Color(1, 1, 1, closeSetting.imageAlpha);
            }
        }
    }

    private void Update()
    {
        if (!counting) return;

        timer += Time.deltaTime;

        if (timer >= closeSetting.resetClickTime)
        {
            ResetClickCount();
        }
    }

    /// <summary>
    /// 터치(클릭) 시 호출됨. 
    /// 클릭 횟수를 증가시키고, 목표치에 도달하면 게임을 종료함.
    /// </summary>
    public void Click()
    {
        counting = true;
        clickCount++;

        if (clickCount >= closeSetting.numToClose)
        {
            Debug.Log("[GameCloser] Force Exit Triggered!");
            GameManager.Instance?.ExitGame();
            ResetClickCount();
        }
    }

    private void ResetClickCount()
    {
        clickCount = 0;
        timer = 0f;
        counting = false;
    }
}