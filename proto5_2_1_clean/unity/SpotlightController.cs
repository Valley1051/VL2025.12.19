using UnityEngine;

/// <summary>
/// スポットライトのマスク制御を行うクラス。
/// 見た目の光（メッシュ）は削除し、X線エフェクト用のマスク機能のみを提供します。
/// </summary>
public class SpotlightController : MonoBehaviour
{
    [Header("Visual Settings")]
    [Range(0f, 1f)] public float transparency = 0.2f; // 透明度スライダー
    public Color visualColor = Color.white;           // 基本色

    [Header("Animation")]
    public Animator targetAnimator;

    private SpriteMask spriteMask;
    private SpriteRenderer spriteRenderer;

    // [New] OSC Control Params
    private OSCReceiver oscReceiver;
    
    [Header("Dynamic Settings")]
    public float targetScale = 1.0f;
    public Vector2 targetPosition = Vector2.zero; // Local Position
    
    // Pythonからの値 (e.g., 50 = Center) をワールド座標に変換するための係数
    // 画面サイズによって調整が必要だが、とりあえず概算値。
    public float posMultiplier = 0.5f; 

    /// <summary>
    /// Start()より前に実行され、Animatorを即座に無効化します。
    /// Unity再生開始時の自動起動を確実に防ぎます。
    /// </summary>
    void Awake()
    {
        // Animatorの取得と即座に無効化
        if (targetAnimator == null) targetAnimator = GetComponent<Animator>();
        
        if (targetAnimator != null)
        {
            targetAnimator.enabled = false;
            Debug.Log("[SpotlightController] Animator disabled in Awake() - will only activate during POSSESSED phase");
        }
    }

    void Start()
    {
        // --- Animatorの取得 ---
        if (targetAnimator == null) targetAnimator = GetComponent<Animator>();
        
        // デフォルトで停止（勝手に動かないようにする）
        if (targetAnimator != null)
        {
            targetAnimator.enabled = false;
        }

        // --- マスクコンポーネントの追加 ---
        spriteMask = GetComponent<SpriteMask>();
        if (spriteMask == null)
        {
            spriteMask = gameObject.AddComponent<SpriteMask>();
            // Layer 4: The Spotlight (Mask)
            // Sorting Order = 500
            spriteMask.frontSortingOrder = 2000;
            spriteMask.backSortingOrder = 500;
        }

        // --- ビジュアル用レンダラーの追加 ---
        // マスクの形を半透明で表示するために SpriteRenderer を追加します
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 501; // マスクと同じくらいの階層
            spriteRenderer.maskInteraction = SpriteMaskInteraction.None; // 自分自身はマスクしない
            
            // Use Additive Shader for "Light" effect
            Shader additive = Shader.Find("SimpleAdditive");
            if (additive == null) additive = Shader.Find("Mobile/Particles/Additive");
            if (additive != null)
            {
                spriteRenderer.material = new Material(additive);
            }
        }
        
        if (spriteMask.sprite == null)
        {
            // グラデーション付き円形スプライトを生成（自然なフェードアウト）
            Texture2D tex = new Texture2D(256, 256);
            Vector2 center = new Vector2(128, 128);
            float radius = 128f;
            
            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float normalizedDist = dist / radius;
                    
                    // グラデーション: 中心100% → 外側0%
                    // 境界をソフトにするため、べき乗を使用
                    float alpha = 1.0f - Mathf.Clamp01(normalizedDist);
                    alpha = Mathf.Pow(alpha, 0.7f); // 0.7でなだらかなグラデーション
                    
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            spriteMask.sprite = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));
            Debug.Log("SpotlightController: Created gradient circle sprite for smooth mask fade.");
        }

        // --- OSC Receiver Setup ---
        oscReceiver = FindObjectOfType<OSCReceiver>();
        if (oscReceiver != null)
        {
            oscReceiver.OnParamReceived += OnParamReceived;
        }
    }

    void OnDestroy()
    {
        if (oscReceiver != null) oscReceiver.OnParamReceived -= OnParamReceived;
    }

    void OnParamReceived(string name, float value)
    {
        // name: spotlightPosX, spotlightPosY, spotlightSize
        if (name == "spotlightPosX")
        {
            // Value assumed 0-100, 50 is center
            // Convert to World X: (val - 50) * multiplier
            targetPosition.x = (value - 50.0f) * posMultiplier;
        }
        else if (name == "spotlightPosY")
        {
            // Value assumed 0-100, 50 is center
            targetPosition.y = (value - 50.0f) * posMultiplier; // Y is usually Up
        }
        else if (name == "spotlightSize")
        {
            // Value assumed 1-50? Default 10?
            // Base scale 1.0 (256px) -> need range 0.5 to 5.0 maybe
            targetScale = Mathf.Clamp(value * 0.1f, 0.1f, 10.0f);
        }
        else if (name == "spotlightAnimEnabled")
        {
            SetPlaying(value > 0.5f);
        }
    }

    // 外部（SkeletonController）から呼ばれる
    public void SetPlaying(bool isPlaying)
    {
        if (targetAnimator != null)
        {
            targetAnimator.enabled = isPlaying;
        }
    }

    void Update()
    {
        // パラメータ適用
        // 位置 (Local Position)
        // アニメーションが再生されていないときだけ適用する？
        // いや、アニメーションはTransformを動かすかもしれない。
        // もしAnimatorがPositionを操作しているなら競合する。
        // DebugモードではAnimatorを止めているはずなので、ここで操作してOK。
        if (targetAnimator == null || !targetAnimator.enabled)
        {
            transform.localPosition = new Vector3(targetPosition.x, targetPosition.y, transform.localPosition.z);
        }

        // スケール
        transform.localScale = new Vector3(targetScale, targetScale, 1.0f);

        // マスク画像とビジュアル画像を同期
        if (spriteMask != null && spriteRenderer != null)
        {
            if (spriteRenderer.sprite != spriteMask.sprite)
            {
                spriteRenderer.sprite = spriteMask.sprite;
            }
            
            // 透明度を適用
            Color c = visualColor;
            c.a = transparency;
            spriteRenderer.color = c;
        }
    }
}
