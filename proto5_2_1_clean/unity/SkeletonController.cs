using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 受信した骨格データを元に、全身の骸骨を描画・制御するクラス。
/// </summary>
public class SkeletonController : MonoBehaviour
{
    public OSCReceiver oscReceiver;
    
    [System.Serializable]
    public class SkeletonAssets
    {
        [Header("Torso & Head")]
        public Sprite skull;      // ID 0
        public Sprite ribcage;    // ID 11-12 Mid
        public Sprite pelvis;     // ID 23-24 Mid
        
        [Header("Arm (Right Source)")]
        public Sprite upperArm;   // 上腕
        public Sprite forearm;    // 前腕
        public Sprite hand;       // 手
        
        [Header("Leg (Right Source)")]
        public Sprite thigh;      // 太もも
        public Sprite shin;       // スネ
        public Sprite foot;       // 足
    }

    public SkeletonAssets assets;

    [Header("Settings")]
    public bool autoCalibrateScale = true; // 自動調整を行うか (デフォルトOFF: 手動調整推奨) -> Debug: Default true

    // ... (existing code) ...

    void OnDrawGizmos()
    {
        if (skeletons == null) return;
        
        foreach (var kvp in skeletons)
        {
            var skel = kvp.Value;
            if (skel == null) continue;
            
            Gizmos.color = (kvp.Key == 0) ? Color.green : Color.red;
            
            // 簡易的にパーツ位置を結ぶ
            // 注: SkeletonInstanceの内部データには直接アクセスしにくい構造なので、
            // Transformを探して描画する
            
            Transform root = transform.Find($"Skeleton_{kvp.Key}");
            if (root == null) continue;
            
            void DrawBone(string start, string end)
            {
                Transform s = FindDeepChild(root, start);
                Transform e = FindDeepChild(root, end);
                if (s != null && e != null)
                {
                    Gizmos.DrawLine(s.position, e.position);
                    Gizmos.DrawSphere(s.position, 0.05f);
                }
            }
            
            // 主要なボーンを描画
            DrawBone("Pelvis", "Thigh_L");
            DrawBone("Thigh_L", "Shin_L");
            DrawBone("Shin_L", "Foot_L");
            
            DrawBone("Pelvis", "Thigh_R");
            DrawBone("Thigh_R", "Shin_R");
            DrawBone("Shin_R", "Foot_R");
            
            DrawBone("Pelvis", "Ribcage");
            DrawBone("Ribcage", "Skull");
            
            DrawBone("Ribcage", "UpperArm_L");
            DrawBone("UpperArm_L", "Forearm_L");
            DrawBone("Forearm_L", "Hand_L");
            
            DrawBone("Ribcage", "UpperArm_R");
            DrawBone("UpperArm_R", "Forearm_R");
            DrawBone("Forearm_R", "Hand_R");
            
            // Debug: Draw Target Lines (Blue) - Calculated positions directly from landmarks
            Gizmos.color = Color.blue;
            if (skel.lastLandmarks != null && skel.lastLandmarks.Count > 32)
            {
                Vector3 GetP(int i) => skel.GetPosForGizmo(i);
                
                void DrawL(int a, int b) { Gizmos.DrawLine(GetP(a), GetP(b)); }
                
                DrawL(11, 12); // Shoulders
                DrawL(11, 23); // Body L
                DrawL(12, 24); // Body R
                DrawL(23, 24); // Hips
                
                DrawL(11, 13); DrawL(13, 15); // Arm L
                DrawL(12, 14); DrawL(14, 16); // Arm R
                
                DrawL(23, 25); DrawL(25, 27); // Leg L
                DrawL(24, 26); DrawL(26, 28); // Leg R
                
                DrawL(0, 11); DrawL(0, 12); // Neckish
            }
        }
    }
    
    Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    public float scaleX = 16.0f; // 手動調整用
    public float scaleY = 9.0f;  // 手動調整用
    public bool useZForSorting = true;
    
    [Header("Manual Adjustment")]
    public Vector2 globalOffset = Vector2.zero; // 手動位置調整
    public float globalScale = 1.0f;            // 手動サイズ調整
    public bool disableMasking = true;         // マスク有効 (暗くなる場合はスポットライトの位置を確認) -> Debug: Default true
    
    [Header("Manual Part Scaling")]
    public float headScaleMultiplier = 1.0f;
    public float bodyScaleMultiplier = 1.0f;
    public float armWidthMultiplier = 1.0f;
    public float legWidthMultiplier = 1.0f;
    public float ribcageVerticalOffset = 0.0f; // 肋骨の上下位置調整
    
    [Header("Visual Tuning")]
    public float boneRotation = 0.0f;           // 骨の回転角度 (リセット: 0)
    public float ghostFadeSpeed = 1.0f;         // 亡霊の出現速度倍率
    public float ghostScaleMultiplier = 1.0f;   // 亡霊のサイズ倍率
    public float swayAmount = 0.0f;             // 揺れの強さ
    public bool spotlightAnimEnabled = false;   // スポットライトアニメーション有効化 (Default OFF)

    [Header("Stadium Wall Layout")]
    public float fanAngle = 150.0f;             // 扇形の角度幅 (背後)
    public float centerGapAngle = 30.0f;        // 中央の隙間角度
    public float rowHeightStep = 2.0f;          // 列ごとの高さ上昇量
    public float rowScaleMultiplier = 1.8f;     // 列ごとの巨大化倍率
    public float randomJitter = 0.5f;           // 配置のランダムズレ

    [Header("Phantom Settings")]
    public Color[] phantomColors = new Color[16];

    private Dictionary<int, SkeletonInstance> skeletons = new Dictionary<int, SkeletonInstance>();
    private string currentPhase = "IDLE";
    private float currentProgress = 0f;

    void Start()
    {
        if (oscReceiver == null) oscReceiver = FindObjectOfType<OSCReceiver>();
        oscReceiver.OnPoseReceived += UpdateSkeleton;
        oscReceiver.OnStateReceived += UpdateState;
        oscReceiver.OnParamReceived += UpdateParam; // パラメータ受信
        
        // 亡霊用のカラーパレット初期化
        if (phantomColors.Length == 0 || phantomColors[0] == Color.clear)
        {
            for (int i = 0; i < 16; i++)
                phantomColors[i] = Color.HSVToRGB((float)i / 16f, 1f, 1f);
        }
        
        // 【座標スケールの自動調整】
        if (autoCalibrateScale && Camera.main != null)
        {
            float camHeight = Camera.main.orthographicSize * 2.0f;
            float camWidth = camHeight * Camera.main.aspect;
            scaleX = camWidth;
            scaleY = camHeight;
            Debug.Log($"Auto-calibrated Scale: X={scaleX}, Y={scaleY} (CamSize={Camera.main.orthographicSize}, Aspect={Camera.main.aspect})");
        }

        // --- 画像アセットの割り当てチェック ---
        if (assets.skull == null || assets.ribcage == null || assets.pelvis == null ||
            assets.upperArm == null || assets.forearm == null || assets.hand == null ||
            assets.thigh == null || assets.shin == null || assets.foot == null)
        {
            Debug.LogError("SkeletonController: Some Sprites are missing! Please assign them in the Inspector.");
        }
    }

    void UpdateState(string state, float progress)
    {
        currentPhase = state;
        currentProgress = progress;
        
        // スポットライトのアニメーション連動
        SpotlightController spotlight = FindObjectOfType<SpotlightController>();
        if (spotlight != null)
        {
            // IDLEとFINISHED以外は「演奏中」とみなす
            bool isPlayingState = (state != "IDLE" && state != "FINISHED");
            // スイッチがONの場合のみアニメーションさせる
            bool shouldPlay = isPlayingState && spotlightAnimEnabled;
            spotlight.SetPlaying(shouldPlay);
        }
    }

    void UpdateParam(string paramName, float value)
    {
        if (paramName == "boneRotation") boneRotation = value;
        else if (paramName == "ghostFadeSpeed") ghostFadeSpeed = value;
        else if (paramName == "ghostScaleMultiplier") ghostScaleMultiplier = value;
        else if (paramName == "swayAmount") swayAmount = value;
        else if (paramName == "spotlightAnimEnabled") 
        {
            spotlightAnimEnabled = (value > 0.5f);
            // Apply immediately
            SpotlightController spotlight = FindObjectOfType<SpotlightController>();
            if (spotlight != null) spotlight.SetPlaying(spotlightAnimEnabled && (currentPhase != "IDLE" && currentPhase != "FINISHED"));
        }
    }

    void UpdateSkeleton(int playerId, float energy, List<Vector4> landmarks)
    {
        // Debug: Check if data is received
        if (playerId == 0 && Time.frameCount % 60 == 0) 
        {
            Debug.Log($"UpdateSkeleton: ID={playerId}, Landmarks={landmarks.Count}, HeadVis={landmarks[0].w}");
        }
        bool isGhost = (playerId > 0);

        // 新しいIDならインスタンス生成
        if (!skeletons.ContainsKey(playerId))
        {
            Vector3 offset = Vector3.zero;
            float scale = 1.0f;
            Color color = Color.white;

            if (isGhost)
            {
                // 亡霊は少し位置をずらして、サイズや色を変える
                float randomX = Random.Range(2.0f, 8.0f);
                if (Random.value > 0.5f) randomX *= -1;
                offset = new Vector3(randomX, 0, 0);
                scale = Random.Range(0.8f, 1.5f);
                color = phantomColors[(playerId - 1) % phantomColors.Length];
            }

            skeletons[playerId] = new SkeletonInstance(playerId, transform, assets, offset, scale, color, isGhost, this);
        }
        
        // フェーズ進行に応じた透明度アニメーション (亡霊のみ)
        float alpha = 1.0f;
        if (isGhost)
        {
            float t = currentProgress * 60.0f * ghostFadeSpeed; 
            if (t < 5f) alpha = 0.0f;
            else if (t < 15f) alpha = Mathf.Lerp(0.0f, 0.5f, (t - 5f) / 10f);
            else if (t < 50f) alpha = Mathf.Lerp(0.5f, 1.0f, (t - 15f) / 35f);
            else alpha = Mathf.Lerp(1.0f, 0.0f, (t - 50f) / 10f);
        }

        int sortingOrder = isGhost ? -500 : 1000;
        
        // Symmetric V Formation Layout
        // プレイヤーの背後に、中心（0度）を空けた状態で、左側と右側に完全に対称に配置
        
        Vector2 manualOffset = globalOffset;
        float manualScale = globalScale;
        float lookAtRotation = 0.0f;
        
        if (isGhost)
        {
            // ID 1-16
            // We need to distribute ID 1-16 into Left/Right wings.
            // Let's split them:
            // IDs 1-8 -> Right Wing (Target 8 ghosts, but layout accommodates 3+3+2=8)
            // IDs 9-16 -> Left Wing
            // User script logic: isLeft check
            
            int ghostIdx = (playerId - 1); // 0-15
            bool isLeft = (ghostIdx >= 8); // 8-15 is Left, 0-7 is Right
            int localIdx = ghostIdx % 8;   // 0-7 per side
            
            // 1. 配置数 (Counts Per Side)
            int[] countPerSide = {3, 3, 2}; // Row 0: 3, Row 1: 3, Row 2: 2
            
            int row = 0;
            int colInRow = 0;
            int maxColsInRow = 3;
            
            // Determine Row and Col based on localIdx
            if (localIdx < countPerSide[0]) 
            {
                row = 0; 
                colInRow = localIdx; 
                maxColsInRow = countPerSide[0]; 
            }
            else if (localIdx < countPerSide[0] + countPerSide[1]) 
            {
                row = 1; 
                colInRow = localIdx - countPerSide[0]; 
                maxColsInRow = countPerSide[1]; 
            }
            else 
            {
                row = 2; 
                colInRow = localIdx - (countPerSide[0] + countPerSide[1]); 
                maxColsInRow = countPerSide[2]; 
            }
            
            // 2. 角度の計算 (Angle Logic)
            // 中央ギャップ (Center Gap) から外側へ扇状に広げる
            float gapAngle = centerGapAngle; // e.g., 20 or 30
            // Fan Range: Use 'fanAngle' as the total angle coverable per side (or total back). 
            // Interpret 'fanAngle' (150) as total coverage approx. So per side ~75 degrees?
            // Let's use a "Fan Sweep" logic: Start at Gap, spread out by some step.
            // Or distribute evenly within a range.
            // "Scatter balancedly" -> distribute t from 0 to 1.
            
            float sideFanRange = (fanAngle - gapAngle) * 0.5f; // E.g., (150-30)/2 = 60 degrees spread per side
            
            // t goes from 0.0 to 1.0 within the row
            float t = (maxColsInRow > 1) ? (float)colInRow / (maxColsInRow - 1) : 0.5f;
            
            // Angle magnitude (absolute from center)
            // Start from GapAngle, go outwards
            float angleMag = (gapAngle * 0.5f) + (t * sideFanRange);
            
            // Apply side sign
            // Right: +Angle, Left: -Angle
            // (Note: In Unity 2D (assuming Top-Down logic for math), usually + is CCW.
            // Current code map: x = radius * Sin(angle). 
            // If angle is 0, x=0.
            // If angle > 0, x > 0 (Right).
            // If angle < 0, x < 0 (Left).
            // So Right side should be Positive Angle, Left side Negative.
            
            float sideSign = isLeft ? -1.0f : 1.0f;
            
            float finalAngleDeg = angleMag * sideSign;
            float angleRad = finalAngleDeg * Mathf.Deg2Rad;
            
            // Radius & Height (Stadium Height)
            // 3. 維持する演出 (Keep Existing)
            float baseRadius = 4.0f;
            float radius = baseRadius + (row * 1.5f); // Increase radius slightly per row
            
            // X position (Spread)
            float x = radius * Mathf.Sin(angleRad);
            
            // Z position (Depth for sorting/parallax) - mapped to Y in 2D usually or Z
            // Original code didn't use Z for position much in Sin calculation, it used purely 2D logic?
            // Original: x = radius * Sin, y = ...
            // Wait, this is 2D. 
            // Let's assume standard "Stadium" means tiered height Y.
            
            // Y position (Height) -> "Stadium Height"
            float groundY = -3.0f;
            float y = groundY + (row * rowHeightStep);
            
            // Scale (Reverse Scale)
            // 奥の列（Row 2）に行くほど、スケールを劇的に巨大にする
            float baseScale = 0.25f;
            float s = baseScale * Mathf.Pow(rowScaleMultiplier, row);
            
            // Jitter
            float jx = (Mathf.PerlinNoise(playerId * 12.34f, 0) - 0.5f) * randomJitter;
            float jy = (Mathf.PerlinNoise(playerId * 56.78f, 100) - 0.5f) * randomJitter;
            
            x += jx;
            y += jy;
            
            // LookAt: 全ての亡霊がプレイヤーを向く
            // In 2D, facing center means rotating effectively by -Angle
            lookAtRotation = -finalAngleDeg;
            
            // Sway Animation
            float progressFactor = Mathf.Clamp01(currentProgress * 2.0f); 
            float totalSway = swayAmount + (progressFactor * 0.5f); 
            
            float time = Time.time;
            float swayX = Mathf.Sin(time * 2.0f + ghostIdx) * totalSway; 
            float swayY = Mathf.Cos(time * 1.5f + ghostIdx) * totalSway * 0.5f;
            
            manualOffset = new Vector2(x + swayX, y + swayY);
            manualScale = s * ghostScaleMultiplier; 
        }

        skeletons[playerId].UpdatePose(landmarks, energy, scaleX, scaleY, useZForSorting, alpha, manualOffset, manualScale, disableMasking, isGhost, sortingOrder, boneRotation + lookAtRotation);
        
        // Ghost Spawning Logic (Simulate IDs 1-16 from ID 0 data)
        // Disabled: Now handled by GhostLayoutManager
        /*
        if (playerId == 0 && currentPhase == "PHANTOM")
        {
            for (int i = 1; i <= 16; i++)
            {
                // Recursive call for ghosts
                // UpdateSkeleton(i, energy, landmarks);
            }
        }
        */
    }

    class SkeletonInstance
    {
        int id;
        GameObject root;
        Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
        SkeletonController controller; // 親コントローラーへの参照
        
        Vector3 baseOffset;
        float baseScale;
        Color baseColor;

        bool isGhost;
        
        public List<Vector4> lastLandmarks; // For Debug Gizmos
        float lastSX, lastSY;
        Vector2 lastGOffset;
        float lastGScale;
        
        float currentSkullScale = 0.5f; // Smoothing for head scale
        float currentRibScale = 1.0f;
        float currentPelvisScale = 1.0f;
        
        public Vector3 GetPosForGizmo(int index)
        {
            if (lastLandmarks == null || index >= lastLandmarks.Count) return Vector3.zero;
            Vector4 lm = lastLandmarks[index];
            float x = ((lm.x - 0.5f) * lastSX * baseScale) * lastGScale + lastGOffset.x;
            float y = (-(lm.y - 0.5f) * lastSY * baseScale) * lastGScale + lastGOffset.y;
            float z = -5.0f;
            return new Vector3(x, y, z);
        }
        
        public SkeletonInstance(int id, Transform parent, SkeletonAssets assets, Vector3 offset, float scale, Color color, bool ghost, SkeletonController ctrl)
        {
            this.id = id;
            this.baseOffset = offset;
            this.baseScale = scale;
            this.baseColor = color;
            this.isGhost = ghost;
            this.controller = ctrl;

            root = new GameObject($"Skeleton_{id}");
            root.transform.SetParent(parent);
            
            // 胴体
            Transform pelvis = CreatePart("Pelvis", assets.pelvis, root.transform, color, false);
            Transform ribcage = CreatePart("Ribcage", assets.ribcage, root.transform, color, false);
            Transform skull = CreatePart("Skull", assets.skull, root.transform, color, false);
            
            // 左手足 (Mirror)
            Transform upperArmL = CreatePart("UpperArm_L", assets.upperArm, root.transform, color, true);
            Transform forearmL = CreatePart("Forearm_L", assets.forearm, root.transform, color, true);
            Transform handL = CreatePart("Hand_L", assets.hand, root.transform, color, true);
            
            Transform thighL = CreatePart("Thigh_L", assets.thigh, root.transform, color, true);
            Transform shinL = CreatePart("Shin_L", assets.shin, root.transform, color, true);
            Transform footL = CreatePart("Foot_L", assets.foot, root.transform, color, true);
            
            // 右手足
            Transform upperArmR = CreatePart("UpperArm_R", assets.upperArm, root.transform, color, false);
            Transform forearmR = CreatePart("Forearm_R", assets.forearm, root.transform, color, false);
            Transform handR = CreatePart("Hand_R", assets.hand, root.transform, color, false);
            
            Transform thighR = CreatePart("Thigh_R", assets.thigh, root.transform, color, false);
            Transform shinR = CreatePart("Shin_R", assets.shin, root.transform, color, false);
            Transform footR = CreatePart("Foot_R", assets.foot, root.transform, color, false);
        }
        
        Transform CreatePart(string name, Sprite sprite, Transform parent, Color color, bool mirror)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent);
            
            if (sprite != null)
            {
                SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = color;
                sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                
                // 両手を上下反転
                if (name == "Hand_L" || name == "Hand_R")
                {
                    sr.flipY = true;
                }
                
                // ユーザーの左手（画面上右、Hand_R）のみ左右反転
                if (name == "Hand_R")
                {
                    sr.flipX = true;
                }
            }
            
            // コンポーネントの追加 (Spriteがなくても追加して、位置更新を行えるようにする)
            if (name == "Skull" || name == "Ribcage" || name == "Pelvis")
            {
                if (obj.GetComponent<BoneRotator>() == null) obj.AddComponent<BoneRotator>();
            }
            else
            {
                if (obj.GetComponent<BoneStretcher>() == null) obj.AddComponent<BoneStretcher>();
            }
            
            parts[name] = obj.transform;
            return obj.transform;
        }


        
        // Silhouette Renderer
        SpriteRenderer silhouetteRenderer;
        VideoReceiver cachedVideoReceiver;

        public void UpdatePose(List<Vector4> landmarks, float energy, float sX, float sY, bool useZ, float alpha, Vector2 gOffset, float gScale, bool noMask, bool isGhost, int sortOrder, float rotationOffset)
        {
            // --- Silhouette Handling for Ghosts ---
            // Disabled: Python side handles ghost silhouettes in the video feed.
            // Rendering it here using VideoReceiver.currentSprite (Live Mask) is incorrect for ghosts.
            if (silhouetteRenderer != null)
            {
                silhouetteRenderer.gameObject.SetActive(false);
            }
            
            /*
            if (isGhost)
            {
                if (silhouetteRenderer == null)
                {
                    // Create Silhouette Object
                    GameObject silObj = new GameObject("GhostSilhouette");
                    silObj.transform.SetParent(root.transform);
                    silhouetteRenderer = silObj.AddComponent<SpriteRenderer>();
                    
                    // Use Additive Shader
                    Shader additive = Shader.Find("SimpleAdditive");
                    if (additive == null) additive = Shader.Find("Mobile/Particles/Additive");
                    if (additive != null) silhouetteRenderer.material = new Material(additive);
                    
                    silhouetteRenderer.maskInteraction = SpriteMaskInteraction.None;
                }

                // Cache VideoReceiver
                if (cachedVideoReceiver == null) cachedVideoReceiver = GameObject.FindObjectOfType<VideoReceiver>();

                if (cachedVideoReceiver != null && cachedVideoReceiver.currentSprite != null)
                {
                    silhouetteRenderer.sprite = cachedVideoReceiver.currentSprite;
                    silhouetteRenderer.gameObject.SetActive(alpha > 0.01f);
                    
                    // Color & Transparency
                    Color c = baseColor;
                    c.a = alpha * 0.5f; // Slightly transparent
                    silhouetteRenderer.color = c;
                    
                    silhouetteRenderer.sortingOrder = sortOrder - 1; // Behind bones

                    // Transform
                    Vector3 originalScale = cachedVideoReceiver.transform.localScale;
                    silhouetteRenderer.transform.localScale = originalScale * gScale;
                    
                    // Z Position: Use 0 or slightly behind main
                    silhouetteRenderer.transform.position = new Vector3(gOffset.x, gOffset.y, 0.0f); 
                }
            }
            else
            {
                // Main Skeleton - No extra silhouette needed (VideoReceiver handles it)
                if (silhouetteRenderer != null) silhouetteRenderer.gameObject.SetActive(false);
            }
            */

            Color targetColor;
            SpriteMaskInteraction maskInteraction;
            
            if (isGhost)
            {
                targetColor = new Color(0.5f, 0.56f, 0.63f, alpha * 0.5f); 
                maskInteraction = SpriteMaskInteraction.None; 
            }
            else
            {
                targetColor = new Color(1f, 1f, 1f, alpha);
                maskInteraction = noMask ? SpriteMaskInteraction.None : SpriteMaskInteraction.VisibleInsideMask;
            }

            foreach(var part in parts.Values)
            {
                SpriteRenderer sr = part.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = targetColor;
                    sr.maskInteraction = maskInteraction;
                    sr.sortingOrder = sortOrder; 
                }
                part.gameObject.SetActive(alpha > 0.01f);
            }
            
            if (landmarks.Count < 33) return;
            
            // Store for Gizmos
            lastLandmarks = landmarks;
            lastSX = sX; lastSY = sY;
            lastGOffset = gOffset; lastGScale = gScale;

            Vector3 GetPos(int index)
            {
                Vector4 lm = landmarks[index];
                float x = ((lm.x - 0.5f) * sX * baseScale) * gScale + gOffset.x;
                float y = (-(lm.y - 0.5f) * sY * baseScale) * gScale + gOffset.y;
                float z = -5.0f; // Force Z to be in front
                return new Vector3(x, y, z);
            }

            // 1. 手足 (BoneStretcher)
            void UpdateLimb(string partName, int index1, int index2, float visibilityThreshold = 0.5f)
            {
                if (!parts.ContainsKey(partName)) return;
                Transform t = parts[partName];
                BoneStretcher stretcher = t.GetComponent<BoneStretcher>();
                if (stretcher == null) return;

                float v1 = landmarks[index1].w;
                float v2 = landmarks[index2].w;
                
                // 視認性チェック: 無効化 (Debug)
                // if (v1 < visibilityThreshold || v2 < visibilityThreshold)
                // {
                //     t.gameObject.SetActive(false);
                //     return;
                // }
                t.gameObject.SetActive(true); // Force Active
                
                Vector3 p1 = GetPos(index1);
                Vector3 p2 = GetPos(index2);
                
                // 回転オフセットのみ適用
                float finalRotation = rotationOffset;

                // Determine width multiplier based on part type
                float widthMult = 1.0f;
                if (partName.Contains("Arm") || partName.Contains("Hand")) widthMult = controller.armWidthMultiplier;
                else if (partName.Contains("Leg") || partName.Contains("Foot") || partName.Contains("Thigh") || partName.Contains("Shin")) widthMult = controller.legWidthMultiplier;

                stretcher.UpdateBone(p1, p2, finalRotation, widthMult);
            }

            // 2. 胴体・頭 (BoneRotator)
            void UpdateBodyPart(string partName, Vector3 position, Vector3 directionPairStart, Vector3 directionPairEnd, float scale = 1.0f, float visibilityThreshold = 0.5f)
            {
                if (!parts.ContainsKey(partName)) return;
                Transform t = parts[partName];
                BoneRotator rotator = t.GetComponent<BoneRotator>();
                if (rotator == null) return;

                // 視認性チェック
                // ここでは代表点(positionの元)のvisibilityは呼び出し元でチェック済みとするが、
                // 方向計算用のペアもチェックすべき。
                // しかし引数で渡されていないので、呼び出し元で制御する。

                // 角度計算: ペア（耳同士、肩同士）の傾き
                Vector3 dir = directionPairEnd - directionPairStart;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                
                // 補正: 画像の向きに合わせて調整
                // User Request: 180 degrees for Head/Rib/Hip
                rotator.UpdatePart(position, angle + 180 + rotationOffset, scale);
            }

            // 視認性の閾値を下げる (YOLO対応: 0.3f推奨)
            float th = 0.3f;

            // --- Head (Skull) ---
            // 鼻(0) が見えていれば表示
            if (landmarks[0].w > th)
            {
                Vector3 nose = GetPos(0);
                Vector3 earL = GetPos(7);
                Vector3 earR = GetPos(8);
                
                float targetScale = currentSkullScale; // Default

                // 耳が見えている場合のみ新しいスケールを計算
                if (landmarks[7].w > th && landmarks[8].w > th)
                {
                    float earDist = Vector3.Distance(earL, earR);
                    SpriteRenderer sr = parts["Skull"].GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite != null)
                    {
                        float spriteWidth = sr.sprite.bounds.size.x;
                        if (spriteWidth > 0) 
                        {
                            float rawScale = (earDist / spriteWidth) * 1.0f;
                            targetScale = rawScale * controller.headScaleMultiplier;
                        }
                    }
                }
                
                // Smoothing Removed
                currentSkullScale = targetScale;

                UpdateBodyPart("Skull", nose, earL, earR, currentSkullScale, th);
            }
            else
            {
                parts["Skull"].gameObject.SetActive(false);
            }

            // --- Ribcage ---
            if (landmarks[11].w > th && landmarks[12].w > th)
            {
                Vector3 sL = GetPos(11);
                Vector3 sR = GetPos(12);
                Vector3 sMid = (sL + sR) * 0.5f;
                
                float targetScale = currentRibScale;

                float shoulderDist = Vector3.Distance(sL, sR);
                SpriteRenderer sr = parts["Ribcage"].GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    float w = sr.sprite.bounds.size.x;
                    if (w > 0) 
                    {
                        float rawScale = (shoulderDist / w) * 1.2f; 
                        targetScale = rawScale * controller.bodyScaleMultiplier;
                    }
                }
                
                // Smoothing Removed
                currentRibScale = targetScale;
                
                // Offset calculation
                Vector3 shoulderDir = (sR - sL).normalized;
                Vector3 spineDir = new Vector3(shoulderDir.y, -shoulderDir.x, 0);
                
                // Apply manual offset (Scaled by RibScale)
                Vector3 finalPos = sMid + (spineDir * controller.ribcageVerticalOffset * currentRibScale);

                UpdateBodyPart("Ribcage", finalPos, sL, sR, currentRibScale, th);
            }
            else
            {
                parts["Ribcage"].gameObject.SetActive(false);
            }

            // --- Pelvis ---
            if (landmarks[23].w > th && landmarks[24].w > th)
            {
                Vector3 hL = GetPos(23);
                Vector3 hR = GetPos(24);
                Vector3 hMid = (hL + hR) * 0.5f;
                
                float targetScale = currentPelvisScale;

                float hipDist = Vector3.Distance(hL, hR);
                SpriteRenderer sr = parts["Pelvis"].GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    float w = sr.sprite.bounds.size.x;
                    if (w > 0) 
                    {
                        float rawScale = (hipDist / w) * 1.3f;
                        targetScale = rawScale * controller.bodyScaleMultiplier;
                    }
                }

                // Smoothing Removed
                currentPelvisScale = targetScale;

                UpdateBodyPart("Pelvis", hMid, hL, hR, currentPelvisScale, th);
            }
            else
            {
                parts["Pelvis"].gameObject.SetActive(false);
            }

            // --- Limbs (BoneStretcher) ---
            UpdateLimb("UpperArm_L", 11, 13, th);
            UpdateLimb("Forearm_L", 13, 15, th);
            UpdateLimb("Hand_L", 15, 19, th);

            UpdateLimb("UpperArm_R", 12, 14, th);
            UpdateLimb("Forearm_R", 14, 16, th);
            UpdateLimb("Hand_R", 16, 20, th);

            UpdateLimb("Thigh_L", 23, 25, th);
            UpdateLimb("Shin_L", 25, 27, th);
            UpdateLimb("Foot_L", 27, 31, th);

            UpdateLimb("Thigh_R", 24, 26, th);
            UpdateLimb("Shin_R", 26, 28, th);
            UpdateLimb("Foot_R", 28, 32, th);
        }
    }
}
