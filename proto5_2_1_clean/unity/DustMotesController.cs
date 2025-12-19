using UnityEngine;

/// <summary>
/// 浮遊する塵（Dust Motes）の制御
/// 古い劇場の映写機のような、空間に漂う埃を表現
/// 
/// ビジュアルコンセプト:
/// - 「誰もいない古い劇場の、映写機の光」
/// - 「暗い屋根裏部屋に差し込む、一筋の陽光」
/// - 空間に固定され、スポットライトが移動してもついてこない
/// </summary>
public class DustMotesController : MonoBehaviour
{
    [Header("Particle Settings")]
    [Tooltip("粒子の総数（100-1000）")]
    [Range(100, 1000)]
    public int maxParticles = 300;
    
    [Tooltip("粒子のサイズ（0.01-0.5）")]
    [Range(0.01f, 0.5f)]
    public float particleSize = 0.05f;
    
    [Header("Color & Appearance")]
    [Tooltip("埃の色（クリーム/アンバー推奨）")]
    public Color dustColor = new Color(1f, 0.96f, 0.86f, 0.4f); // RGB(255, 245, 220), Alpha 0.4
    
    [Header("Motion - Brownian Movement")]
    [Tooltip("初速度（0.001-0.1）")]
    [Range(0.001f, 0.1f)]
    public float initialSpeed = 0.02f;
    
    [Tooltip("横方向の浮遊速度（0-0.2）")]
    [Range(0f, 0.2f)]
    public float driftSpeedX = 0.05f;
    
    [Tooltip("縦方向の浮遊速度（0-0.2）")]
    [Range(0f, 0.2f)]
    public float driftSpeedY = 0.03f;
    
    [Tooltip("ノイズ（揺らぎ）の強さ（0-1）")]
    [Range(0f, 1f)]
    public float noiseStrength = 0.5f;
    
    [Tooltip("ノイズの周波数（0.1-1）")]
    [Range(0.1f, 1f)]
    public float noiseFrequency = 0.3f;
    
    [Tooltip("ノイズのスクロール速度")]
    [Range(0f, 0.5f)]
    public float noiseScrollSpeed = 0.1f;
    
    [Header("Spatial Configuration")]
    [Tooltip("パーティクル発生範囲（幅）")]
    public float emissionWidth = 20f;
    
    [Tooltip("パーティクル発生範囲（高さ）")]
    public float emissionHeight = 10f;
    
    [Header("Layer Order")]
    [Tooltip("描画順序（1500推奨：骸骨とシルエットの間）")]
    public int sortingOrder = 1500;
    
    private ParticleSystem ps;
    private ParticleSystemRenderer psRenderer;
    
    void Start()
    {
        SetupParticleSystem();
        CreateDustMaterial();
        
        Debug.Log("[DustMotes] Initialized - World-space dust particles with Brownian motion");
    }
    
    void SetupParticleSystem()
    {
        // パーティクルシステムの取得または作成
        ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            ps = gameObject.AddComponent<ParticleSystem>();
        }
        
        psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        
        // === Main Module ===
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // ★ワールド座標固定（最重要）
        main.maxParticles = maxParticles;
        main.duration = 20f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 20f); // 長寿命
        main.startSpeed = new ParticleSystem.MinMaxCurve(initialSpeed * 0.5f, initialSpeed * 1.5f); // Inspector調整可能
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.5f, particleSize * 1.5f); // サイズのバリエーション
        main.startColor = dustColor;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad); // ランダム回転
        
        // === Emission ===
        var emission = ps.emission;
        emission.rateOverTime = maxParticles / 10f; // 10秒かけて徐々に生成
        
        // === Shape - 画面全体に配置 ===
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(emissionWidth, emissionHeight, 0.1f);
        
        // === Velocity over Lifetime - 非常に微妙な浮遊（大幅に低速化） ===
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World; // ワールド空間での速度
        velocity.x = new ParticleSystem.MinMaxCurve(-driftSpeedX, driftSpeedX); // Inspector調整可能
        velocity.y = new ParticleSystem.MinMaxCurve(-driftSpeedY, driftSpeedY); // Inspector調整可能
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // Z軸も同じモードに統一（2D空間のため0）
        
        // === Noise Module - ブラウン運動 ===
        var noise = ps.noise;
        noise.enabled = true;
        noise.separateAxes = false;
        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = noiseScrollSpeed;
        noise.damping = true;
        noise.octaveCount = 2; // 適度な複雑さ
        noise.quality = ParticleSystemNoiseQuality.High;
        
        // === Size over Lifetime - 微妙な明滅 ===
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.8f);   // 開始時はやや小さめ
        sizeCurve.AddKey(0.5f, 1.0f); // 中間で最大
        sizeCurve.AddKey(1f, 0.8f);   // 終了時にまたやや小さく
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        
        // === Color over Lifetime - 微妙な明滅 ===
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.3f, 0f),   // フェードイン
                new GradientAlphaKey(1.0f, 0.3f),
                new GradientAlphaKey(1.0f, 0.7f),
                new GradientAlphaKey(0.3f, 1f)    // フェードアウト
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        
        // === Renderer Settings ===
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        psRenderer.sortingOrder = sortingOrder;
        psRenderer.alignment = ParticleSystemRenderSpace.View; // カメラに対して常に正面
        psRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; // スポットライト内でのみ表示（X線効果）
        
        Debug.Log($"[DustMotes] Particle System configured: {maxParticles} particles, World-space simulation, Mask interaction enabled");
    }
    
    void CreateDustMaterial()
    {
        // Additiveシェーダーを探す
        Shader shader = Shader.Find("Mobile/Particles/Additive");
        if (shader == null) shader = Shader.Find("Particles/Additive");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
        
        if (shader != null)
        {
            Material mat = new Material(shader);
            
            // ソフトな円形テクスチャを生成
            Texture2D tex = CreateSoftCircleTexture(64);
            mat.mainTexture = tex;
            
            psRenderer.material = mat;
            
            Debug.Log("[DustMotes] Material created with soft dust particle texture");
        }
        else
        {
            Debug.LogWarning("[DustMotes] Additive shader not found. Using default material.");
        }
    }
    
    /// <summary>
    /// ソフトな円形のテクスチャを生成（埃の粒子用）
    /// </summary>
    Texture2D CreateSoftCircleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float normalizedDist = dist / radius;
                
                // ソフトなグラデーション（べき乗で柔らかく）
                float alpha = Mathf.Clamp01(1f - normalizedDist);
                alpha = Mathf.Pow(alpha, 2.5f); // 外側に向かって急速に減衰
                
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        
        tex.Apply();
        return tex;
    }
    
    /// <summary>
    /// Inspectorから呼び出せる再初期化メソッド
    /// </summary>
    [ContextMenu("Reinitialize Dust Motes")]
    public void Reinitialize()
    {
        if (ps != null)
        {
            DestroyImmediate(ps);
        }
        Start();
    }
}
