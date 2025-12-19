using UnityEngine;

/// <summary>
/// 背景画像を表示・制御するクラス。
/// 指定された画像を画面いっぱいに表示し、最背面に配置します。
/// </summary>
public class BackgroundController : MonoBehaviour
{
    [Header("Settings")]
    public Sprite backgroundImage;
    public int sortingOrder = -2000; // 最背面
    public Color color = Color.white;

    private GameObject bgObject;
    private SpriteRenderer sr;

    void Start()
    {
        CreateBackground();
    }

    void CreateBackground()
    {
        // 子オブジェクトとして作成
        bgObject = new GameObject("BackgroundImage");
        bgObject.transform.SetParent(this.transform);
        bgObject.transform.localPosition = Vector3.zero;

        sr = bgObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;

        if (backgroundImage == null)
        {
            // 画像がない場合は真っ黒にする (1x1の白テクスチャを作成して黒く塗る)
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            sr.color = Color.black;
        }
        else
        {
            sr.sprite = backgroundImage;
            sr.color = color;
        }

        // 画面サイズに合わせてスケール調整 (Aspect Fill)
        FitToScreen();
    }

    void FitToScreen()
    {
        if (sr == null || Camera.main == null) return;

        // カメラのワールド座標系での高さと幅
        float camHeight = Camera.main.orthographicSize * 2.0f;
        float camWidth = camHeight * Camera.main.aspect;

        // 画像のサイズ
        float spriteHeight = sr.sprite.bounds.size.y;
        float spriteWidth = sr.sprite.bounds.size.x;

        // スケール計算 (Aspect Fill: 大きい方に合わせる)
        float scaleY = camHeight / spriteHeight;
        float scaleX = camWidth / spriteWidth;
        float scale = Mathf.Max(scaleX, scaleY);

        bgObject.transform.localScale = new Vector3(scale, scale, 1.0f);
    }

    // インスペクターで画像を変更したときに即座に反映（エディタ実行中のみ）
#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying && bgObject != null && sr != null)
        {
            sr.sprite = backgroundImage;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            FitToScreen();
        }
    }
#endif
}
