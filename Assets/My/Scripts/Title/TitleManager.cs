using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class TitleSetting
{
    public ImageSetting titleBackgroundImage;
    public ButtonSetting titleStartButton;
}

public sealed class TitleManager : BaseManager<TitleSetting>
{   
    [Header("UI")]
    [SerializeField] private GameObject titleBackgroundImage;
    [SerializeField] private GameObject titleStartButton;
    
    protected override string JsonPath => "JSON/TitleSetting.json";
   
    protected override async UniTask Initialize()
    {
        try
        {
            ui.SetImageObj(titleBackgroundImage, managerSetting.titleBackgroundImage);
            await ui.SetButtonObj(titleStartButton, managerSetting.titleStartButton, DestroyToken);
            
            Button startBtn = titleStartButton != null ? titleStartButton.GetComponent<Button>() : null;
            if (startBtn != null)
            {
                startBtn.onClick.RemoveListener(OnStartButtonClicked);
                startBtn.onClick.AddListener(OnStartButtonClicked);
            }
            
            await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[TitleManager] => Initialize Canceled");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TitleManager] => Initialize Exception: {e}");
        }
        finally
        {
            Debug.Log("[TitleManager] => Initialize Finished");
        }
    }
    
    private void OnStartButtonClicked()
    {
        HandleStartButtonAsync().Forget();
    }
    
    private async UniTask HandleStartButtonAsync()
    {
        try
        {
            await fader.FadeOut(fadeImage, fadeTime, DestroyToken);

            AsyncOperation op = SceneManager.LoadSceneAsync("LevelSelect", LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[TitleManager] HandleStartButtonAsync-> LoadSceneAsync returned null");
                return;
            }
            
            while (!op.isDone)
            {
                DestroyToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, DestroyToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 정상으로 넘어감
        }
        catch (Exception e)
        {
            Debug.LogError($"[TitleManager] HandleStartButtonAsync-> Exception: {e}");
        }
    }
}