using UnityEngine;
// URP (Universal Render Pipeline) ではなく、標準の Post Processing Stack v2 を使用するように変更
// Window > Package Manager > Unity Registry > "Post Processing" をインストールしてください。
using UnityEngine.Rendering.PostProcessing; 

/// <summary>
/// 「生命のボリューム」を制御するクラス。
/// ユーザーの運動量（Energy）に応じて、画面のBloom（発光）や彩度を変化させます。
/// Unity 2D (Built-in Render Pipeline) 向けに Post Processing Stack v2 を使用します。
/// </summary>
public class LifeVolume : MonoBehaviour
{
    public OSCReceiver oscReceiver;
    public PostProcessVolume globalVolume; // PostProcessVolumeに変更
    
    [Header("Settings")]
    public float energySmoothing = 5.0f; 
    public float minBloom = 0.0f;        
    public float maxBloom = 10.0f;       // PPv2のBloomは強度が異なるため調整
    public float minSaturation = -100f;  
    public float maxSaturation = 0f;     
    
    private float currentEnergy = 0f;
    private float targetEnergy = 0f;
    
    // Post Processing v2のエフェクト参照
    private Bloom bloom;
    private ColorGrading colorGrading; // ColorAdjustmentsの代わりにColorGrading

    void Start()
    {
        if (oscReceiver == null) oscReceiver = FindObjectOfType<OSCReceiver>();
        oscReceiver.OnPoseReceived += HandlePose;
        
        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGetSettings(out bloom);
            globalVolume.profile.TryGetSettings(out colorGrading);
        }
    }

    void HandlePose(int playerId, float energy, System.Collections.Generic.List<Vector4> landmarks)
    {
        if (playerId == 0) 
        {
            targetEnergy = energy;
        }
    }

    void Update()
    {
        currentEnergy = Mathf.Lerp(currentEnergy, targetEnergy, Time.deltaTime * energySmoothing);
        
        // 0.0 (静止) 〜 2.0 (激しい動き) を想定
        float t = Mathf.InverseLerp(0.0f, 2.0f, currentEnergy);
        
        if (bloom != null)
        {
            bloom.intensity.value = Mathf.Lerp(minBloom, maxBloom, t);
        }
        
        if (colorGrading != null)
        {
            colorGrading.saturation.value = Mathf.Lerp(minSaturation, maxSaturation, t);
        }
    }
}
