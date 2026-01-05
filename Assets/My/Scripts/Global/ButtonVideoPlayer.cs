using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 버튼 배경에 비디오를 재생하는 헬퍼 클래스.
/// UIManager에서 버튼 배경이 동영상일 경우 자동으로 추가하여 사용.
/// </summary>
[RequireComponent(typeof(RawImage), typeof(VideoPlayer))]
public class ButtonVideoPlayer : MonoBehaviour
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
        _videoPlayer.renderMode = VideoRenderMode.APIOnly; // 텍스처를 직접 제어
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // 버튼 배경이므로 소리는 끔
    }

    /// <summary> URL(파일 경로)로 비디오 재생 </summary>
    public async UniTask PlayVideoAsync(string url, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(url)) return;

        // 이미 같은 비디오가 재생 중이면 리턴
        if (_videoPlayer.isPlaying && _videoPlayer.url == url) return;

        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.url = url;

        // 준비 시작
        _videoPlayer.Prepare();
        
        // 준비 완료 대기
        while (!_videoPlayer.isPrepared)
        {
            if (token.IsCancellationRequested) return;
            await UniTask.Yield(PlayerLoopTiming.Update);
        }

        // 텍스처 연결 및 재생
        _rawImage.texture = _videoPlayer.texture;
        _rawImage.color = Color.white; // 텍스처가 잘 보이도록 흰색 설정
        _videoPlayer.Play();
    }

    /// <summary> 비디오 정지 및 숨김 </summary>
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