using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public abstract class BaseManager<TSetting> : MonoBehaviour
{   
    [Header("Fade Image")]
    [SerializeField] protected Image fadeImage;

    private Settings setting;
    protected TSetting managerSetting;
    protected abstract string JsonPath { get; }

    protected UIManager ui;
    protected FadeManager fader;
    protected CancellationToken DestroyToken => this.GetCancellationTokenOnDestroy();

    protected float fadeTime;
    
    protected virtual async void Start()
    {
        try
        {
            if (JsonLoader.Instance == null) return;
            if (JsonLoader.Instance.settings != null) setting = JsonLoader.Instance.settings; // setting.json 설정
            else return;

            if (UIManager.Instance != null) ui = UIManager.Instance; // UIManager 저장
            else return;

            if (FadeManager.Instance != null) fader = FadeManager.Instance;
            else return;

            managerSetting = JsonLoader.Instance.LoadJsonData<TSetting>(JsonPath); // 각 매니저 세팅 클래스 설정
            fadeTime = setting.fadeTime;
            
            await Initialize();
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning($"[{SceneManager.GetActiveScene().name}] => Start Canceled");
        }
        catch (Exception e)
        {
            Debug.LogError($"[{SceneManager.GetActiveScene().name}] => Start Exception: {e}");
        }
        finally
        {
            Debug.Log($"[{SceneManager.GetActiveScene().name}] => Start Complete");
        }
    }
    
    protected abstract UniTask Initialize();
}
