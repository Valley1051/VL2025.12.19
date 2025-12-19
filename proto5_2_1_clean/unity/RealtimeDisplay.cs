using UnityEngine;

/// <summary>
/// 単純にテクスチャを受け取って表示するだけのクラス。
/// ロジックは持たず、言われた通りに描画することに専念する。
/// </summary>
public class RealtimeDisplay : MonoBehaviour
{
    private Renderer myRenderer;

    void Awake()
    {
        myRenderer = GetComponent<Renderer>();
        if (myRenderer == null)
        {
            Debug.LogError("RealtimeDisplay: No Renderer found on this object!");
        }
    }

    /// <summary>
    /// 外部（Manager）からテクスチャを受け取って適用する
    /// </summary>
    /// <param name="texture">表示すべきテクスチャ</param>
    public void SetTexture(Texture texture)
    {
        if (myRenderer != null && texture != null)
        {
            myRenderer.material.mainTexture = texture;
        }
    }
}
