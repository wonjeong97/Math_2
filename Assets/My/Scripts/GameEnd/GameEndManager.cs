using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

public static class GameResultContext
{
    /// <summary> 맞춘 문제 개수 (0 ~ 4) </summary>
    public static int CorrectCount { get; set; } = 0;
}

[Serializable]
public class GameEndSetting
{
    public ImageSetting gameEndImage;
    public ImageSetting[] scoreResultImages;
    public ButtonSetting homeButton;
}

public class GameEndManager : MonoBehaviour
{
    [Header("UI Objects")]
    [SerializeField] private Image uiGameEndImage;  // 배경/타이틀
    [SerializeField] private Image uiMyScoreImage;  // 점수 결과가 표시될 이미지
    [SerializeField] private Button uiHomeButton;   // 홈 버튼
    
    private GameEndSetting _setting;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // 1. JSON 데이터 로드
        if (JsonLoader.Instance != null)
        {
            // 파일 경로는 실제 위치에 맞춰 수정해주세요 (예: JSON/GameEnd.json)
            _setting = JsonLoader.Instance.LoadJsonData<GameEndSetting>("JSON/GameEnd.json");
        }

        if (_setting == null)
        {
            Debug.LogError("[GameEndManager] Setting Data is null.");
            return;
        }

        ApplyUISettings();
        SetupButtonEvents();
    }

    private void ApplyUISettings()
    {
        if (UIManager.Instance == null) return;

        // 1. 기본 배경/타이틀 이미지 설정
        if (uiGameEndImage != null && _setting.gameEndImage != null)
        {
            UIManager.Instance.SetImageObj(uiGameEndImage.gameObject, _setting.gameEndImage);
        }

        // 2. 홈 버튼 설정
        if (uiHomeButton != null && _setting.homeButton != null)
        {
            UIManager.Instance.SetButtonObj(uiHomeButton.gameObject, _setting.homeButton).Forget();
        }

        // 3. 점수(Score) 이미지 설정 (핵심 로직)
        if (uiMyScoreImage != null && _setting.scoreResultImages != null)
        {
            // 전달받은 점수 가져오기 (0 ~ 4 사이로 클램핑하여 예외 방지)
            int score = Mathf.Clamp(GameResultContext.CorrectCount, 0, 4);

            // 설정된 배열 길이 확인
            if (score < _setting.scoreResultImages.Length)
            {
                ImageSetting scoreImgData = _setting.scoreResultImages[score];
                if (scoreImgData != null)
                {
                    UIManager.Instance.SetImageObj(uiMyScoreImage.gameObject, scoreImgData);
                }
            }
            else
            {
                Debug.LogWarning($"[GameEndManager] Score {score}에 해당하는 이미지 세팅이 없습니다.");
            }
        }
    }

    private void SetupButtonEvents()
    {
        if (uiHomeButton != null)
        {
            uiHomeButton.onClick.RemoveAllListeners();
            uiHomeButton.onClick.AddListener(() =>
            {
                // 타이틀(홈) 씬으로 이동
                SceneManager.LoadScene("Title"); 
            });
        }
    }
}
