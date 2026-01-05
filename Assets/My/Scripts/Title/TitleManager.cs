using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 설정 데이터 클래스.
/// 배경, 시작 버튼, 타이틀 텍스트 설정을 포함.
/// </summary>
[Serializable]
public class TitleSetting
{
    public ImageSetting titleBackgroundImage;
    public ButtonSetting titleStartButton;
    public TextSetting titleText1;
    public TextSetting titleText2;
}

/// <summary>
/// 타이틀(시작) 화면을 관리하는 매니저.
/// UI 초기화 및 레벨 선택 씬으로의 전환을 담당.
/// </summary>
public sealed class TitleManager : BaseManager<TitleSetting>
{   
    [Header("UI")]
    [SerializeField] private GameObject titleBackgroundImage; // 배경 이미지
    [SerializeField] private GameObject titleStartButton;     // 시작 버튼
    [SerializeField] private GameObject titleText1;           // 타이틀 텍스트 1 (메인)
    [SerializeField] private GameObject titleText2;           // 타이틀 텍스트 2 (서브)
    
    // JSON 파일 경로
    protected override string JsonPath => "JSON/TitleSetting.json";
   
    /// <summary>
    /// 초기화 진입점.
    /// UI(이미지, 텍스트, 버튼) 설정 및 페이드 인 실행.
    /// </summary>
    protected override async UniTask Initialize()
    {
        try
        {   
            // 타이틀 씬 진입 시 BGM 정지.
            SoundManager.Instance?.PlayBGM(null);
            
            // UI 설정 적용
            ui.SetImageObj(titleBackgroundImage, managerSetting.titleBackgroundImage);
            ui.SetTextObj(titleText1, managerSetting.titleText1).Forget();
            ui.SetTextObj(titleText2, managerSetting.titleText2).Forget();
            await ui.SetButtonObj(titleStartButton, managerSetting.titleStartButton, DestroyToken);
            
            // 시작 버튼 이벤트 연결
            Button startBtn = titleStartButton != null ? titleStartButton.GetComponent<Button>() : null;
            if (startBtn != null)
            {
                startBtn.onClick.RemoveListener(OnStartButtonClicked);
                startBtn.onClick.AddListener(OnStartButtonClicked);
            }
            
            // 화면 페이드 인
            await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[Title] => Initialize Canceled");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Title] => Initialize Exception: {e}");
        }
        finally
        {
            Debug.Log("[Title] => Initialize Finished");
        }
    }
    
    /// <summary> 시작 버튼 클릭 핸들러 (비동기 래퍼). </summary>
    private void OnStartButtonClicked()
    {   
        //SoundManager.Instance?.PlaySFX("Button");
        HandleStartButtonAsync().Forget();
    }
    
    /// <summary>
    /// 시작 버튼 로직.
    /// 페이드 아웃 후 'LevelSelect' 씬으로 이동.
    /// </summary>
    private async UniTask HandleStartButtonAsync()
    {
        try
        {
            // 페이드 아웃
            await fader.FadeOut(fadeImage, fadeTime, DestroyToken);
            
            Debug.Log("[Title] Player Clicked Start");
            
            // 씬 비동기 로드
            AsyncOperation op = SceneManager.LoadSceneAsync("LevelSelect", LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[Title] HandleStartButtonAsync-> LoadSceneAsync returned null");
                return;
            }
            
            // 로드 완료 대기
            while (!op.isDone)
            {
                DestroyToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, DestroyToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 씬 전환 중 취소됨 (정상 흐름)
        }
        catch (Exception e)
        {
            Debug.LogError($"[Title] HandleStartButtonAsync-> Exception: {e}");
        }
    }
}