using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// UI(RawImage)에 비디오를 재생하는 범용 클래스.
/// 배경 화면, 일반 이미지 등에서 동영상을 재생할 때 사용.
/// </summary>
[RequireComponent(typeof(RawImage), typeof(VideoPlayer))]
public class UIVideoPlayer : MonoBehaviour
{
    private VideoPlayer _videoPlayer;
    private RawImage _rawImage;

    private void Awake()
    {
        _videoPlayer = GetComponent<VideoPlayer>();
        _rawImage = GetComponent<RawImage>();

        // 비디오 플레이어 기본 설정
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = true;
        _videoPlayer.renderMode = VideoRenderMode.APIOnly; 
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // 오디오가 필요한 경우 외부에서 설정
    }

    public async UniTask PlayVideoAsync(string url, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (_videoPlayer.isPlaying && _videoPlayer.url == url) return;

        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.url = url;

        _videoPlayer.Prepare();

        float timeout = 10f; // 10초 타임아웃
        float elapsed = 0f;
        while (!_videoPlayer.isPrepared)
        {
            if (token.IsCancellationRequested) return;
            
            elapsed += Time.deltaTime;
            if (elapsed > timeout)
            {
                Debug.LogError($"[UIVideoPlayer] Video preparation timeout: {url}");
                return;
            }
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        _rawImage.texture = _videoPlayer.texture;
        _videoPlayer.Play();
    }

    public void Stop()
    {
        if (_videoPlayer.isPlaying) _videoPlayer.Stop();
        if (_rawImage) _rawImage.enabled = false;
    }

    public void SetColor(Color color)
    {
        if (_rawImage) _rawImage.color = color;
    }
}