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
    private bool _isTransitioning; 

    private void Awake()
    {
        if (!Instance)
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
        Input.simulateMouseWithTouches = false;
        Cursor.visible = false;

        if (reporter && reporter.show)
        {
            reporter.show = false;
        }
    }

    private void Update()
    {   
        if (Input.GetKeyDown(KeyCode.D))
        {
            reporter.showGameManagerControl = !reporter.showGameManagerControl;
            if (reporter.show) reporter.show = false;
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {
            Cursor.visible = !Cursor.visible;
        }
        
        if (_isTransitioning) return;

        HandleInactivity();
    }

    /// <summary>
    /// 외부 입력 감지 없이 순수하게 시간만 누적시키며, 지정된 특정 버튼 이벤트에서만 타이머를 리셋함.
    /// (허위 터치나 지정되지 않은 UI 클릭으로 인한 오작동 방지)
    /// </summary>
    private void HandleInactivity()
    {
        if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title)
        {
            _currentInactivityTimer = 0f;
            return;
        }

        _currentInactivityTimer += Time.deltaTime;

        float limitTime = 60f; 
        if (JsonLoader.Instance && JsonLoader.Instance.settings != null)
        {
            limitTime = JsonLoader.Instance.settings.inactivityTime;
        }

        if (_currentInactivityTimer >= limitTime)
        {
            ReturnToTitle();
        }
    }

    /// <summary>
    /// 지정된 게임 내 주요 버튼(레벨 선택, 정답 제출, 홈 버튼 등)을 클릭했을 때 호출되어 타이머를 초기화함.
    /// </summary>
    public void ResetInactivityTimer()
    {
        _currentInactivityTimer = 0f;
    }

    /// <summary> 타이틀 씬으로 복귀 절차를 시작함. </summary>
    public void ReturnToTitle()
    {
        if (_isTransitioning) return; 

        _isTransitioning = true;
        _currentInactivityTimer = 0f;
        
        Debug.Log("Inactivity Detected: Returning to Title");
        ReturnToTitleAsync().Forget();
    }

    /// <summary> 페이드 아웃 연출 후 타이틀 씬을 로드함. </summary>
    private async UniTaskVoid ReturnToTitleAsync()
    {
        GameObject fadeObj = GameObject.Find("FadeImage");
        Image fadeImage = fadeObj ? fadeObj.GetComponent<Image>() : null;

        float fadeTime = 1.0f;
        if (JsonLoader.Instance && JsonLoader.Instance.settings != null)
        {
            fadeTime = JsonLoader.Instance.settings.fadeTime;
        }
        else
        {
            Debug.LogWarning("JsonLoader settings is null. Using default fadeTime.");
        }

        if (FadeManager.Instance && fadeImage)
        {
            await FadeManager.Instance.FadeOut(fadeImage, fadeTime, this.GetCancellationTokenOnDestroy());
        }

        SceneManager.LoadScene(GameConstants.Scene.Title);

        await UniTask.Yield(); 
        _isTransitioning = false;
        _currentInactivityTimer = 0f;
    }

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