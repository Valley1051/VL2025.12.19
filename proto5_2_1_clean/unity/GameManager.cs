using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ゲームの進行状態（フェーズ）を管理し、イベントを発火させるクラス。
/// Pythonから送られてくるステート情報に基づきます。
/// </summary>
public class GameManager : MonoBehaviour
{
    public OSCReceiver oscReceiver;
    
    [Header("Phase Events")]
    // Unityエディタ上で、各フェーズ開始時に実行したい処理（パーティクル再生など）を登録できます
    public UnityEvent OnIntroStart;
    public UnityEvent OnAMelodyStart;
    public UnityEvent OnChorusStart;
    public UnityEvent OnOutroStart;
    public UnityEvent OnFinished;
    
    [Header("Debug")]
    public string currentPhase = "IDLE";
    public float currentProgress = 0f;

    void Start()
    {
        if (oscReceiver == null) oscReceiver = FindObjectOfType<OSCReceiver>();
        oscReceiver.OnStateReceived += HandleState;
    }

    // ステート受信時の処理
    void HandleState(string state, float progress)
    {
        // フェーズが切り替わった瞬間のみイベントを実行
        if (currentPhase != state)
        {
            currentPhase = state;
            switch (state)
            {
                case "INTRO": OnIntroStart.Invoke(); break;
                case "A_MELODY": OnAMelodyStart.Invoke(); break;
                case "CHORUS": OnChorusStart.Invoke(); break;
                case "OUTRO": OnOutroStart.Invoke(); break;
                case "FINISHED": OnFinished.Invoke(); break;
            }
        }
        currentProgress = progress;
    }
}
