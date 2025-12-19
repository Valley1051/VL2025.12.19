using UnityEngine;

public class BoneRotator : MonoBehaviour
{
    public void UpdatePart(Vector3 position, float angle, float scale = 1.0f)
    {
        // 1. 位置 (Position): 指定位置に配置
        transform.localPosition = position;

        // 2. 回転 (Rotation): 指定角度に設定
        transform.localRotation = Quaternion.Euler(0, 0, angle);

        // 3. スケール (Scale): 指定スケール (デフォルト1.0)
        // 頭などは少し大きくしたい場合があるため、引数で受け取る
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
