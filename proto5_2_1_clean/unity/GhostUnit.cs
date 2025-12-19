using UnityEngine;

/// <summary>
/// 個々の亡霊オブジェクトを制御するクラス。
/// Colorの適用、テクスチャ切り替え、および揺らめきアニメーションを担当。
/// </summary>
public class GhostUnit : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private Transform target;
    private MaterialPropertyBlock propBlock;

    // Animation Params
    private float swaySpeed;
    private float swayAmount;
    private float randomSeed;
    
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

    public void Initialize(Transform targetTransform)
    {
        this.target = targetTransform;
    }

    public void SetColor(Color color)
    {
        if (meshRenderer == null) return;
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_TintColor", color);
        meshRenderer.SetPropertyBlock(propBlock);
    }

    public void SetUV(Vector2 offset, Vector2 scale)
    {
        if (meshRenderer == null) return;
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetVector("_MainTex_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
        meshRenderer.SetPropertyBlock(propBlock);
    }

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

    // --- Animation Setters ---

    public void SetSwayParams(float speed, float amount, float seed)
    {
        this.swaySpeed = speed;
        this.swayAmount = amount;
        this.randomSeed = seed;
    }

    public void SetBasePosition(Vector3 pos)
    {
        this.basePosition = pos;
    }

    public void SetTilt(float angle)
    {
        this.tiltAngle = angle;
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

        // 2. Sway Logic (Vertical Sine Wave)
        // 基準位置に対して上下に揺らぎを加算する
        if (swayAmount > 0.001f)
        {
            float swayY = Mathf.Sin((Time.time * swaySpeed) + randomSeed) * swayAmount;
            transform.localPosition = basePosition + new Vector3(0, swayY, 0);
        }
        else
        {
            transform.localPosition = basePosition;
        }
    }
}
