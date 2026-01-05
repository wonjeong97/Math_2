using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary> UI(RawImage)에 비디오를 재생하는 범용 클래스. </summary>
[RequireComponent(typeof(RawImage), typeof(VideoPlayer))]
public class UIVideoPlayer : MonoBehaviour
{
    private VideoPlayer _videoPlayer;
    private RawImage _rawImage;
    
    // 원래 설정하려던 색상을 기억할 변수
    private Color _targetColor = Color.white;

    private void Awake()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
        _rawImage = GetComponent<RawImage>();

        // 비디오 플레이어 기본 설정
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = true;
        _videoPlayer.renderMode = VideoRenderMode.APIOnly; 
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
    }

    public async UniTask PlayVideoAsync(string url, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(url)) return;
        
        // 이미 재생 중인 URL이면 무시 
        if (_videoPlayer.isPlaying && _videoPlayer.url == url) return;

        // 1. 준비되는 동안 화면을 투명하게 숨김
        _rawImage.color = Color.clear;

        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.url = url;

        try
        {
            _videoPlayer.Prepare();

            float timeout = 10f;
            float elapsed = 0f;

            // 2. 준비 대기
            while (!_videoPlayer.isPrepared)
            {
                if (token.IsCancellationRequested)
                {
                    _rawImage.color = _targetColor;
                    return;
                }
                
                elapsed += Time.deltaTime;
                if (elapsed > timeout)
                {
                    Debug.LogError($"[UIVideoPlayer] Video preparation timeout: {url}");
                    _rawImage.color = _targetColor;
                    return;
                }
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // 3. 준비 완료: 텍스처 연결 및 색상 복구
            _rawImage.texture = _videoPlayer.texture;
            _rawImage.color = _targetColor; // 설정해둔 색상으로 복구
            
            _videoPlayer.Play();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UIVideoPlayer] Error playing video: {e.Message}");
            _rawImage.color = _targetColor; 
        }
    }

    public void Stop()
    {
        if (_videoPlayer.isPlaying) _videoPlayer.Stop();
        if (_rawImage) _rawImage.enabled = false;
    }

    public void SetColor(Color color)
    {
        // 외부에서 색상을 설정하면 변수에 저장해둠
        _targetColor = color;
        
        // 만약 이미 재생 중이라면 즉시 적용
        if (_videoPlayer.isPrepared && _rawImage.texture != null)
        {
            _rawImage.color = color;
        }
    }
}