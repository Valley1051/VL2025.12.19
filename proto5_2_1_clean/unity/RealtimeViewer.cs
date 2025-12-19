using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class RealtimeViewer : MonoBehaviour
{
    public VideoReceiver sourceReceiver;
    private SpriteRenderer _myRenderer;
    
    // Grid Logic
    private Rect lastTextureRect;
    // private int gridCols = 4; // Unused
    // private int gridRows = 4; // Unused

    void Start()
    {
        _myRenderer = GetComponent<SpriteRenderer>();
        if (_myRenderer == null) {
            _myRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        // 1. ビジュアル設定 (Master Prompt準拠)
        // 色: 真っ白 (スポットライト外で視認させるため)
        _myRenderer.color = Color.white;
        
        // マスク: スポットライト (Mask) の外でのみ表示 = ライトの中では消える (レントゲン)
        _myRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        
        // レイヤー順序: 骨格(-5)より手前
        _myRenderer.sortingOrder = 10; 

        if (sourceReceiver == null) sourceReceiver = FindObjectOfType<VideoReceiver>();
    }

    void Update()
    {
        // 2. 強制Z座標ロック (Z=-6)
        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.z - (-6.0f)) > 0.01f)
        {
            transform.position = new Vector3(pos.x, pos.y, -6.0f);
        }

        // 3. テクスチャ更新 & グリッド切り出し
        if (sourceReceiver != null && sourceReceiver.texture != null)
        {
            Texture2D tex = sourceReceiver.texture;

            // 初期化ガード
            if (tex.width <= 16) 
            {
                if (_myRenderer != null) _myRenderer.enabled = false;
                return;
            }

            if (_myRenderer == null) return;
            _myRenderer.enabled = true;

            // テクスチャサイズが変わったときだけスプライト再生成
            Rect currentRect = new Rect(0, 0, tex.width, tex.height);
            if (currentRect != lastTextureRect)
            {
                // Full Texture (No Grid Slicing)
                Vector2 pivot = new Vector2(0.5f, 0.5f);
                _myRenderer.sprite = Sprite.Create(tex, currentRect, pivot);
                
                lastTextureRect = currentRect;

                // 4. オートスケール調整
                if (Camera.main != null)
                {
                    float camHeight = Camera.main.orthographicSize * 2.0f;
                    float spriteHeight = tex.height / 100.0f; // Default PPU 100
                    float scale = camHeight / spriteHeight;
                    transform.localScale = new Vector3(scale, scale, 1.0f);
                }
            }
        }
    }
}
