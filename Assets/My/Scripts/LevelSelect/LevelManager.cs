using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class LevelSetting
{
    public ImageSetting levelBackgroundImage;
    public ButtonSetting levelOneButton;
    public ButtonSetting levelTwoButton;
    public ButtonSetting levelThreeButton;
}

public class LevelManager : BaseManager<LevelSetting>
{
    [Header("UI")]
    [SerializeField] private GameObject levelBackgroundImage;
    [SerializeField] private GameObject levelOneButton;
    [SerializeField] private GameObject levelTwoButton;
    [SerializeField] private GameObject levelThreeButton;
    
    protected override string JsonPath => "JSON/LevelSetting.json";
    
      protected override async UniTask Initialize()
    {
        try
        {
            ui.SetImageObj(levelBackgroundImage, managerSetting.levelBackgroundImage);
            await ui.SetButtonObj(levelOneButton,  managerSetting.levelOneButton, DestroyToken);
            await ui.SetButtonObj(levelTwoButton, managerSetting.levelTwoButton, DestroyToken);
            await ui.SetButtonObj(levelThreeButton, managerSetting.levelThreeButton, DestroyToken);
            
            Button levelOneBtn = levelOneButton != null ? levelOneButton.GetComponent<Button>() : null;
            if (levelOneBtn != null)
            {
                levelOneBtn.onClick.RemoveListener(OnLevelOneButtonClicked);
                levelOneBtn.onClick.AddListener(OnLevelOneButtonClicked);
            }
            
            Button levelTwoBtn = levelTwoButton != null ? levelTwoButton.GetComponent<Button>() : null;
            if (levelTwoBtn != null)
            {
                levelTwoBtn.onClick.RemoveListener(OnLevelTwoButtonClicked);
                levelTwoBtn.onClick.AddListener(OnLevelTwoButtonClicked);
            }
            
            Button levelThreeBtn = levelThreeButton != null ? levelThreeButton.GetComponent<Button>() : null;
            if (levelThreeBtn != null)
            {
                levelThreeBtn.onClick.RemoveListener(OnLevelThreeButtonClicked);
                levelThreeBtn.onClick.AddListener(OnLevelThreeButtonClicked);
            }
            
            await fader.FadeIn(fadeImage, fadeTime, DestroyToken);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[LevelManager] => Initialize Canceled");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelManager] => Initialize Exception: {e}");
        }
        finally
        {
            Debug.Log("[LevelManager] => Initialize Finished");
        }
    }
    
    private void OnLevelOneButtonClicked()
    {
        HandleLevelButtonClicked(1).Forget();
    }
    
    private void OnLevelTwoButtonClicked()
    {
        HandleLevelButtonClicked(2).Forget();
    }
    
    private void OnLevelThreeButtonClicked()
    {
        HandleLevelButtonClicked(3).Forget();
    }
    
    private async UniTask HandleLevelButtonClicked(int level)
    {
        try
        {
            await fader.FadeOut(fadeImage, fadeTime, DestroyToken);

            string levelString;
            switch (level)
            {
                case 1:
                    levelString = "LevelOne";
                    break;
                case 2:
                    levelString = "LevelTwo";
                    break;
                case 3:
                    levelString = "LevelThree";
                    break;
                default:
                    levelString = "Title";
                    break;
            }
            
            Debug.Log($"[LevelManager] Player Selected {levelString}");
            AsyncOperation op = SceneManager.LoadSceneAsync(levelString, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[LevelManager] HandleStartButtonAsync-> LoadSceneAsync returned null");
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
            Debug.LogError($"[LevelManager] HandleStartButtonAsync-> Exception: {e}");
        }
    }
}
