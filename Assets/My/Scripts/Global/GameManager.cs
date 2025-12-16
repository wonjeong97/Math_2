using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    [SerializeField] private Reporter reporter;

    private float _currentInactivityTimer;
    private bool _isTransitioning; // 씬 전환 중인지 체크

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Cursor.visible = false;
    }

    private void Update()
    {   
        // D키를 눌러 디버그 패널 활성화 / 비활성화
        if (Input.GetKeyDown(KeyCode.D))
        {
            reporter.showGameManagerControl = !reporter.showGameManagerControl;

            if (reporter.show)
            {
                reporter.show = false;
            }
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {
            Cursor.visible = !Cursor.visible;
        }

        
        // 씬 전환 중이면 입력 체크 중단
        if (_isTransitioning) return;

        HandleInactivity();
    }

    /// <summary> 플레이어의 입력을 감지하고, 일정 시간 입력이 없으면 타이틀로 이동. </summary>
    private void HandleInactivity()
    {
        // Title 씬에서는 작동하지 않음
        if (SceneManager.GetActiveScene().name == "Title")
        {
            _currentInactivityTimer = 0f;
            return;
        }

        // 입력 감지
        bool isInputDetected = Input.anyKey || Input.touchCount > 0;

        if (isInputDetected)
        {
            _currentInactivityTimer = 0f;
        }
        else
        {
            _currentInactivityTimer += Time.deltaTime;

            float limitTime = 60f; 
            
            if (JsonLoader.Instance != null && JsonLoader.Instance.settings != null)
            {
                limitTime = JsonLoader.Instance.settings.inactivityTime;
            }

            if (_currentInactivityTimer >= limitTime)
            {
                ReturnToTitle();
            }
        }
    }

    /// <summary> 타이틀 씬으로 돌아간다. </summary>
    public void ReturnToTitle()
    {
        if (_isTransitioning) return; // 이미 진행 중이면 무시

        _isTransitioning = true;
        _currentInactivityTimer = 0f;
        
        Debug.Log("Inactivity Detected: Returning to Title...");
        ReturnToTitleAsync().Forget();
    }

    /// <summary> 페이드 아웃 효과 후 타이틀 씬 로드 </summary>
    private async UniTaskVoid ReturnToTitleAsync()
    {
        // 1. 현재 씬에서 "FadeImage" 찾기
        // (GameManager는 파괴되지 않으므로, 씬에 있는 FadeImage를 직접 찾아야 함)
        GameObject fadeObj = GameObject.Find("FadeImage");
        Image fadeImage = fadeObj ? fadeObj.GetComponent<Image>() : null;

        // 2. 페이드 시간 가져오기
        float fadeTime = 1.0f;
        if (JsonLoader.Instance && JsonLoader.Instance.settings != null)
        {
            fadeTime = JsonLoader.Instance.settings.fadeTime;
        }

        // 3. 페이드 아웃 실행
        if (FadeManager.Instance  && fadeImage )
        {
            await FadeManager.Instance.FadeOut(fadeImage, fadeTime, this.GetCancellationTokenOnDestroy());
        }

        // 4. 씬 이동
        SceneManager.LoadScene("Title");

        // 5. 상태 초기화
        await UniTask.Yield(); // 한 프레임 대기
        _isTransitioning = false;
        _currentInactivityTimer = 0f;
    }

    /// <summary> 게임을 종료합니다. </summary>
    public void ExitGame()
    {
        Debug.Log("Game Exit Triggered");
        
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}