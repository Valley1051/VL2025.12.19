using UnityEngine;

/// <summary>
/// 音楽管理システム
/// フェーズに応じて待機BGMと体験中の音楽を切り替える
/// </summary>
public class MusicManager : MonoBehaviour
{
    [Header("Music Clips")]
    [Tooltip("待機中（IDLE）に流れるBGM")]
    public AudioClip idleBGM;
    
    [Tooltip("体験中（POSSESSED/COOLDOWN）に流れる音楽")]
    public AudioClip experienceBGM;

    [Header("Settings")]
    [Tooltip("音楽の音量（0.0 ~ 1.0）")]
    [Range(0f, 1f)]
    public float volume = 0.7f;
    
    [Tooltip("フェードイン/アウトの時間（秒）")]
    public float fadeTime = 2.0f;
    
    [Tooltip("待機BGMをループ再生する")]
    public bool loopIdleBGM = true;
    
    [Tooltip("体験音楽をループ再生する")]
    public bool loopExperienceBGM = false;

    private AudioSource audioSource;
    private OSCReceiver oscReceiver;
    private string currentPhase = "IDLE";
    private float targetVolume = 0f;
    private float currentVolume = 0f;
    private bool isFading = false;
    private AudioClip pendingClip = null;

    void Start()
    {
        // AudioSource コンポーネントを取得または追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // AudioSource の初期設定
        audioSource.playOnAwake = false;
        audioSource.loop = loopIdleBGM;
        audioSource.volume = 0f;

        // OSCReceiver に登録
        oscReceiver = FindObjectOfType<OSCReceiver>();
        if (oscReceiver != null)
        {
            oscReceiver.OnStateReceived += OnStateReceived;
        }

        // 待機BGMを開始
        if (idleBGM != null)
        {
            audioSource.clip = idleBGM;
            audioSource.loop = loopIdleBGM;
            audioSource.Play();
            StartFadeIn();
        }
    }

    void OnDestroy()
    {
        if (oscReceiver != null)
        {
            oscReceiver.OnStateReceived -= OnStateReceived;
        }
    }

    void Update()
    {
        // フェード処理
        if (isFading)
        {
            currentVolume = Mathf.MoveTowards(currentVolume, targetVolume, (volume / fadeTime) * Time.deltaTime);
            audioSource.volume = currentVolume;

            // フェード完了
            if (Mathf.Approximately(currentVolume, targetVolume))
            {
                isFading = false;

                // フェードアウト完了後、次の音楽に切り替え
                if (targetVolume == 0f && pendingClip != null)
                {
                    audioSource.clip = pendingClip;
                    audioSource.loop = (pendingClip == idleBGM) ? loopIdleBGM : loopExperienceBGM;
                    audioSource.Play();
                    pendingClip = null;
                    StartFadeIn();
                }
            }
        }
    }

    void OnStateReceived(string state, float progress)
    {
        if (currentPhase == state) return;

        currentPhase = state;
        Debug.Log($"[MusicManager] Phase changed to: {state}");

        switch (state)
        {
            case "IDLE":
                // 待機BGMに切り替え
                if (idleBGM != null && audioSource.clip != idleBGM)
                {
                    SwitchMusic(idleBGM);
                }
                break;

            case "POSSESSED":
            case "COOLDOWN":
                // 体験音楽に切り替え
                if (experienceBGM != null && audioSource.clip != experienceBGM)
                {
                    SwitchMusic(experienceBGM);
                }
                break;
        }
    }

    void SwitchMusic(AudioClip newClip)
    {
        if (newClip == null) return;

        pendingClip = newClip;
        StartFadeOut();
    }

    void StartFadeIn()
    {
        targetVolume = volume;
        isFading = true;
    }

    void StartFadeOut()
    {
        targetVolume = 0f;
        isFading = true;
    }

    /// <summary>
    /// 音量を即座に変更（Inspector からの調整用）
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (!isFading)
        {
            audioSource.volume = volume;
        }
    }

    /// <summary>
    /// 音楽を一時停止
    /// </summary>
    public void Pause()
    {
        audioSource.Pause();
    }

    /// <summary>
    /// 音楽を再開
    /// </summary>
    public void Resume()
    {
        audioSource.UnPause();
    }

    /// <summary>
    /// 音楽を停止
    /// </summary>
    public void Stop()
    {
        audioSource.Stop();
        currentVolume = 0f;
        audioSource.volume = 0f;
        isFading = false;
    }
}
