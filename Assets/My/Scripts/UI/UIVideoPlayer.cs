using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary> UI(RawImage)에 비디오를 재생함. </summary>
[RequireComponent(typeof(RawImage), typeof(VideoPlayer))]
public class UIVideoPlayer : MonoBehaviour
{
    private VideoPlayer _videoPlayer;
    private RawImage _rawImage;
    
    private Color _targetColor = Color.white;
    private string _currentUrl; 
    private CancellationTokenSource _enableCts;

    private void Awake()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
        _rawImage = GetComponent<RawImage>();

        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = true;
        _videoPlayer.renderMode = VideoRenderMode.APIOnly; 
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
    }

    private void OnEnable()
    {   
        _enableCts?.Cancel();
        _enableCts = new CancellationTokenSource();
        
        if (!string.IsNullOrEmpty(_currentUrl))
        {
            PlayVideoAsync(_currentUrl, _enableCts.Token).Forget();
        }
    }

    private void OnDisable()
    {
        _enableCts?.Cancel();
    }

    private void OnDestroy()
    {
        _enableCts?.Cancel();
        _enableCts?.Dispose();
    }

    /// <summary> 비디오를 로드하고 재생함. (비동기) </summary>
    public async UniTask PlayVideoAsync(string url, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (this == null || _videoPlayer == null || _rawImage == null) return;

        _currentUrl = url; 

        if (!gameObject.activeInHierarchy) return;

        if (_videoPlayer.isPlaying && _videoPlayer.url == url)
        {
            _rawImage.color = _targetColor;
            return;
        }

        _rawImage.color = Color.clear;
        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.url = url;

        try
        {
            _videoPlayer.Prepare();

            float timeout = 10f;
            float elapsed = 0f;

            // 준비 대기
            while (!_videoPlayer.isPrepared)
            {
                if (this == null || _videoPlayer == null) return;

                if (token.IsCancellationRequested || !gameObject.activeInHierarchy) return;
                
                elapsed += Time.deltaTime;
                if (elapsed > timeout)
                {
                    Debug.LogError($"[UIVideoPlayer] Video preparation timeout: {url}");
                    if (this != null && _rawImage != null) _rawImage.color = _targetColor; 
                    if (this != null && _videoPlayer != null) _videoPlayer.Stop();
                    return;
                }
                
                await UniTask.Yield(PlayerLoopTiming.Update);
                
                if (this == null || _videoPlayer == null || _rawImage == null) return;
            }

            // 재생 시작
            if (this != null && _videoPlayer != null && _rawImage != null)
            {
                _rawImage.texture = _videoPlayer.texture;
                _rawImage.color = _targetColor; 
                _videoPlayer.Play();
            }
        }
        catch (System.Exception e)
        {
            if (this != null) 
            {
                Debug.LogError($"[UIVideoPlayer] Error playing video: {e.Message}");
                if (_rawImage != null) _rawImage.color = _targetColor; 
            }
        }
    }

    public void Stop()
    {
        if (this == null) return;
        if (_videoPlayer != null && _videoPlayer.isPlaying) _videoPlayer.Stop();
        if (_rawImage != null) _rawImage.enabled = false;
    }

    public void SetColor(Color color)
    {
        if (this == null || _rawImage == null) return;
        
        _targetColor = color;
        if (_rawImage.color != Color.clear)
        {
            _rawImage.color = color;
        }
    }
}