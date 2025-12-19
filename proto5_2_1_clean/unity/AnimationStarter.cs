using UnityEngine;

/// <summary>
/// UnityEventからアニメーションを簡単に再生するためのヘルパースクリプト。
/// Inspectorでアニメーション名を設定し、Play()を呼び出すだけで再生できます。
/// </summary>
public class AnimationStarter : MonoBehaviour
{
    public Animator animator;
    public string animationName = "IntroAnim"; // 再生したいアニメーション名

    void Start()
    {
        // もしAnimatorが未設定なら、同じオブジェクトから探す
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    /// <summary>
    /// 設定された名前のアニメーションを再生します。
    /// UnityEvent (OnIntroStartなど) からこの関数を呼び出してください。
    /// </summary>
    public void Play()
    {
        if (animator != null)
        {
            animator.Play(animationName);
            Debug.Log($"AnimationStarter: Playing '{animationName}'");
        }
        else
        {
            Debug.LogWarning("AnimationStarter: Animator is not assigned!");
        }
    }
}
