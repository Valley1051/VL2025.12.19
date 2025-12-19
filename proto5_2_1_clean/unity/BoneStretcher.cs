using UnityEngine;

public class BoneStretcher : MonoBehaviour
{
    public void UpdateBone(Vector3 start, Vector3 end, float rotationOffset, float widthScale = 1.0f)
    {
        // 1. 位置 (Position): 2点の中点に配置
        // UnityのSpriteは通常Center Pivotなので、中点に置くのが最も自然
        Vector3 midPoint = (start + end) * 0.5f;
        transform.localPosition = midPoint;

        // 2. 回転 (Rotation): 始点から終点への向き
        // 画像は「縦長（上が頭、下が足）」で作られている（Vertical）。
        // Unityの2D回転 0度は「右」を向く。
        // 縦長画像（上向き）を「右」に向けるには -90度回転が必要。
        // したがって、目標角度(angle)に対して -90度 補正する。
        
        Vector3 direction = end - start;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // ユーザー要望により -90 補正 (Vertical Sprite対応)
        transform.localRotation = Quaternion.Euler(0, 0, angle - 90 + rotationOffset);

        // 3. 伸縮 (Scale): 距離に合わせてY軸スケール (縦向き画像前提)
        float distance = direction.magnitude;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        
        if (sr != null && sr.sprite != null)
        {
            float spriteHeight = sr.sprite.bounds.size.y;
            
            if (spriteHeight > 0)
            {
                // Y軸（縦）を距離に合わせる
                float scaleY = Mathf.Max(distance / spriteHeight, 0.1f); 
                
                // X軸（太さ）: アスペクト比を維持しつつ、過剰な太さを防ぐ
                // 手や足が巨大化するのを防ぐため、最大値を制限するか、補正をかける
                float scaleX = scaleY * widthScale;
                
                // 手足（Hand/Foot）の場合は少し小さくするハック (Deprecated: Use widthScale instead)
                // if (gameObject.name.Contains("Hand") || gameObject.name.Contains("Foot")) ...
                
                transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
        }
        else
        {
            if (Time.frameCount % 120 == 0) Debug.LogWarning($"BoneStretcher: {name} has NO Sprite or Renderer!");
        }
    }
}
