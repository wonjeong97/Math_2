using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// UI(RawImage)에 비디오를 재생하는 범용 클래스.
/// 오브젝트가 활성화될 때마다 자동으로 비디오를 재생.
/// </summary>
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

    public async UniTask PlayVideoAsync(string url, CancellationToken token = default)
    {
        // 1. 기본 유효성 검사
        if (string.IsNullOrEmpty(url)) return;
        // 이미 파괴된 경우 중단
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

            // 2. 준비 대기 루프
            while (!_videoPlayer.isPrepared)
            {
                // 대기 도중 컴포넌트가 파괴되었는지 체크
                if (this == null || _videoPlayer == null) return;

                if (token.IsCancellationRequested || !gameObject.activeInHierarchy) return;
                
                elapsed += Time.deltaTime;
                if (elapsed > timeout)
                {
                    Debug.LogError($"[UIVideoPlayer] Video preparation timeout: {url}");
                    // 타임아웃 시에도 살아있는지 체크 후 복구
                    if (this != null && _rawImage != null) _rawImage.color = _targetColor; 
                    if (this != null && _videoPlayer != null) _videoPlayer.Stop();
                    return;
                }
                
                // 프레임 대기
                await UniTask.Yield(PlayerLoopTiming.Update);
                
                //  await 직후 파괴 여부를 다시 체크
                if (this == null || _videoPlayer == null || _rawImage == null) return;
            }

            // 3. 준비 완료 후 적용 및 체크
            if (this != null && _videoPlayer != null && _rawImage != null)
            {
                _rawImage.texture = _videoPlayer.texture;
                _rawImage.color = _targetColor; 
                _videoPlayer.Play();
            }
        }
        catch (System.Exception e)
        {
            // 로그는 남기되, 객체 파괴로 인한 에러(MissingReferenceException)는 무시해도 됨
            if (this != null) // 살아있는데 에러가 난 경우만 로그 출력
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