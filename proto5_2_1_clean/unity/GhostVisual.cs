using UnityEngine;

/// <summary>
/// Ver 5.0: 個々の亡霊オブジェクトを制御するクラス。
/// MaterialPropertyBlockによる安全なAlpha制御、Energy連動の揺れアニメーション、
/// 召喚演出用の個体制御機能を実装。
/// </summary>
public class GhostVisual : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private Transform target;
    private MaterialPropertyBlock propBlock;

    // --- アニメーションパラメータ ---
    private float baseSwaySpeed = 1.0f;
    private float swayAmount = 0.5f;
    private float randomSeed = 0f;
    private float currentSwayPhase = 0f; // 位相の蓄積（カクつき防止）
    
    // --- Energy連動 ---
    private float energyLevel = 0f;
    private float energySmooth = 0f; // スムージング用
    
    // --- 召喚演出用 ---
    [HideInInspector] public int ghostIndex; // 召喚順序判定用
    private float targetAlpha = 1.0f; // 召喚進行度に応じた目標Alpha
    private Color baseColor = Color.white; // ベースカラー（Manager設定）
    
    // --- 位置制御 ---
    [HideInInspector] public Vector3 basePosition; // Managerから指定された基本位置
    public float tiltAngle; // Z-axis rotation

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        propBlock = new MaterialPropertyBlock();
    }

    public void Initialize(Transform targetTransform, int index)
    {
        this.target = targetTransform;
        this.ghostIndex = index;
        this.randomSeed = index * 12.34f; // 個体ごとに異なる位相
    }

    /// <summary>
    /// ベースカラーを設定（RGB値のみ、Alpha制御は別途）
    /// </summary>
    public void SetColor(Color color)
    {
        this.baseColor = color;
        UpdateMaterialProperties();
    }

    /// <summary>
    /// UVオフセットとスケールを設定（4x4グリッド用）
    /// </summary>
    public void SetUV(Vector2 offset, Vector2 scale)
    {
        if (meshRenderer == null) return;
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetVector("_MainTex_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
        meshRenderer.SetPropertyBlock(propBlock);
    }

    /// <summary>
    /// テクスチャを設定
    /// </summary>
    public void SetTexture(Texture texture)
    {
        if (meshRenderer == null) return;

        if (texture != null)
        {
            if (!meshRenderer.enabled) meshRenderer.enabled = true;
            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetTexture("_MainTex", texture);
            meshRenderer.SetPropertyBlock(propBlock);
        }
        else
        {
            if (meshRenderer.enabled) meshRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 揺れパラメータを設定
    /// </summary>
    public void SetSwayParams(float speed, float amount, float seed)
    {
        this.baseSwaySpeed = speed;
        this.swayAmount = amount;
        this.randomSeed = seed;
    }

    /// <summary>
    /// ベース位置を設定
    /// </summary>
    public void SetBasePosition(Vector3 pos)
    {
        this.basePosition = pos;
    }

    /// <summary>
    /// 傾き角度を設定
    /// </summary>
    public void SetTilt(float angle)
    {
        this.tiltAngle = angle;
    }

    /// <summary>
    /// Energy値を設定（風の共鳴用）
    /// </summary>
    public void SetEnergyLevel(float energy)
    {
        this.energyLevel = Mathf.Clamp01(energy);
    }

    /// <summary>
    /// 召喚演出用のAlpha値を設定（0.0 ~ 1.0）
    /// </summary>
    public void SetSummonAlpha(float alpha)
    {
        this.targetAlpha = Mathf.Clamp01(alpha);
        UpdateMaterialProperties();
    }

    /// <summary>
    /// MaterialPropertyBlockを使用して、既存シェーダー設定を保護しつつAlphaを制御
    /// </summary>
    private void UpdateMaterialProperties()
    {
        if (meshRenderer == null) return;

        // 既存のPropertyBlockを取得
        meshRenderer.GetPropertyBlock(propBlock);

        // ベースカラーにAlphaを適用
        // IMPORTANT: シェーダー内で「col.a *= i.color.a」処理があるため、
        // ここで設定したAlpha値がシェーダー側の足元フェード等と乗算される
        Color finalColor = baseColor;
        finalColor.a = targetAlpha; // 召喚Alphaを適用

        propBlock.SetColor("_TintColor", finalColor);
        meshRenderer.SetPropertyBlock(propBlock);
    }

    void Update()
    {
        // 1. Billboard Logic with Tilt
        if (Camera.main != null)
        {
            // Face Camera
            transform.rotation = Quaternion.LookRotation(-Camera.main.transform.forward, Vector3.up);
            // Apply Tilt (Z-axis rotation)
            transform.Rotate(0, 0, tiltAngle, Space.Self);
        }

        // 2. Energy Smoothing（急激な変化を防ぐ）
        energySmooth = Mathf.Lerp(energySmooth, energyLevel, Time.deltaTime * 3.0f);

        // 3. Sway Logic with Energy Response
        if (swayAmount > 0.001f)
        {
            // Energy値で揺れ速度を調整（0.5倍 ~ 1.5倍）
            float energyMultiplier = 1.0f + (energySmooth - 0.5f);
            float effectiveSpeed = baseSwaySpeed * energyMultiplier;
            
            // 位相を蓄積（カクつき防止）
            currentSwayPhase += Time.deltaTime * effectiveSpeed;
            
            float swayY = Mathf.Sin(currentSwayPhase + randomSeed) * swayAmount;
            transform.localPosition = basePosition + new Vector3(0, swayY, 0);
        }
        else
        {
            transform.localPosition = basePosition;
        }
    }
}
