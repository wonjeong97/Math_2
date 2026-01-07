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

        if (reporter && reporter.show)
        {
            reporter.show = false;
        }
    }

    private void Update()
    {   
        // D키: 디버그 패널 토글
        if (Input.GetKeyDown(KeyCode.D))
        {
            reporter.showGameManagerControl = !reporter.showGameManagerControl;
            if (reporter.show) reporter.show = false;
        }
        // M키: 마우스 커서 토글
        else if (Input.GetKeyDown(KeyCode.M))
        {
            Cursor.visible = !Cursor.visible;
        }
        
        if (_isTransitioning) return;

        HandleInactivity();
    }

    /// <summary> 입력이 없으면 타이틀로 이동시킴. </summary>
    private void HandleInactivity()
    {
        if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title)
        {
            _currentInactivityTimer = 0f;
            return;
        }

        bool isInputDetected = Input.anyKey || Input.touchCount > 0;

        if (isInputDetected)
        {
            _currentInactivityTimer = 0f;
        }
        else
        {
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
    }

    /// <summary> 타이틀 씬으로 복귀함. </summary>
    public void ReturnToTitle()
    {
        if (_isTransitioning) return; 

        _isTransitioning = true;
        _currentInactivityTimer = 0f;
        
        Debug.Log("Inactivity Detected: Returning to Title");
        ReturnToTitleAsync().Forget();
    }

    /// <summary> 페이드 아웃 후 타이틀 씬을 로드함. </summary>
    private async UniTaskVoid ReturnToTitleAsync()
    {
        GameObject fadeObj = GameObject.Find("FadeImage");
        Image fadeImage = fadeObj ? fadeObj.GetComponent<Image>() : null;

        float fadeTime = 1.0f;
        if (JsonLoader.Instance && JsonLoader.Instance.settings != null)
        {
            fadeTime = JsonLoader.Instance.settings.fadeTime;
        }

        if (FadeManager.Instance  && fadeImage )
        {
            await FadeManager.Instance.FadeOut(fadeImage, fadeTime, this.GetCancellationTokenOnDestroy());
        }

        SceneManager.LoadScene(GameConstants.Scene.Title);

        await UniTask.Yield(); 
        _isTransitioning = false;
        _currentInactivityTimer = 0f;
    }

    /// <summary> 게임을 종료함. </summary>
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