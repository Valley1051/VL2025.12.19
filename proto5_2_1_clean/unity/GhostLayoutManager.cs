using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 亡霊システムのマネージャー。
/// 【Ver 3.2 Dual Stream対応】
/// 亡霊用の映像は、独立したVideoReceiver (Port 5007) から取得する。
/// これにより、体験者のリアルタイム映像(Port 5006)と干渉せず、ループ映像(Grid)のみを表示する。
/// </summary>
public class GhostLayoutManager : MonoBehaviour
{
    [Header("References")]
    public VideoReceiver mainVideoReceiver; 
    public Transform playerTransform; 
    
    // Internal dedicated receiver for Ghosts (Port 5007)
    private VideoReceiver ghostVideoReceiver;

    [Header("Ghost Settings")]
    public Material ghostMaterial;
    public float baseGhostSize = 2.0f;          
    public float ghostScaleMultiplier = 1.0f;
    public Vector3 globalOffset = Vector3.zero;
    
    [Header("Texture Buffering")]
    [Range(0, 120)] public int ghostDelayFrames = 5; 
    private Queue<RenderTexture> textureBuffer = new Queue<RenderTexture>();
    private RenderTexture activeGhostTexture;

    [Header("Layout Parameters (Symmetric V)")]
    public float baseRadius = 10.0f;
    public float rowDepthStep = 5.0f;
    
    public float[] leftRowHeights = new float[] { 0f, 2f, 4f };
    public float[] rightRowHeights = new float[] { 0f, 2f, 4f };
    public float[] leftRowScales = new float[] { 1f, 1.5f, 3.0f };
    public float[] rightRowScales = new float[] { 1f, 1.5f, 3.0f };
    
    public float[] leftRowGaps = new float[] { 15f, 15f, 15f }; 
    public float[] rightRowGaps = new float[] { 15f, 15f, 15f };
    public float[] leftRowCenterDists = new float[] { 15f, 30f, 45f };
    public float[] rightRowCenterDists = new float[] { 15f, 30f, 45f };

    [Header("Ghost Configuration")]
    [Range(0, 32)] public int totalGhosts = 16; 
    private List<GhostUnit> ghosts = new List<GhostUnit>(); 

    private OSCReceiver oscReceiver;
    public float teamGapX = 0.0f;

    [Header("Animation")]
    public float tiltMultiplier = 0.5f; // 傾きの強さ
    public float swayAmount = 0.5f;
    public float swaySpeed = 1.0f;
    
    [Header("UI Overlay")]
    public GameObject cooldownOverlay; // クールタイム中に表示する画像など（Inspectorで設定）

    [Header("Summoning (Ver 5.0)")]
    public bool enableSummoning = false;
    private string currentPhase = "IDLE";
    private float summonStartTime = 0f;
    public float summonDurationPerRow = 1.0f; // 1列あたりの召喚時間（秒）
    
    // 16体それぞれの召喚開始タイミング（秒）
    // Inspector で個別に調整可能
    public float[] ghostSummonDelays = new float[16] {
        // Right側 (0-7)
        0.0f, 0.3f, 0.6f,  // Row 0 (手前)
        1.0f, 1.3f, 1.6f,  // Row 1 (中段)
        2.0f, 2.3f,        // Row 2 (奥)
        // Left側 (8-15)
        0.0f, 0.3f, 0.6f,  // Row 0
        1.0f, 1.3f, 1.6f,  // Row 1
        2.0f, 2.3f         // Row 2
    };
    public float summonFadeDuration = 0.5f; // 各Ghostのフェードイン時間（秒）
    
    private float currentEnergy = 0f; // OSCから受信したEnergy値
    private Color[] ghostColors = new Color[16]; // 各Ghostの基本色を保存

    void Start()
    {
        // Parameter Initialization
        if (leftRowHeights == null || leftRowHeights.Length < 3) leftRowHeights = new float[] { 0f, 2f, 4f };
        if (rightRowHeights == null || rightRowHeights.Length < 3) rightRowHeights = new float[] { 0f, 2f, 4f };
        
        if (leftRowScales == null || leftRowScales.Length < 3) leftRowScales = new float[] { 1f, 1.5f, 3.0f };
        if (rightRowScales == null || rightRowScales.Length < 3) rightRowScales = new float[] { 1f, 1.5f, 3.0f };
        
        if (leftRowGaps == null || leftRowGaps.Length < 3) leftRowGaps = new float[] { 15f, 15f, 15f };
        if (rightRowGaps == null || rightRowGaps.Length < 3) rightRowGaps = new float[] { 15f, 15f, 15f };
        
        if (leftRowCenterDists == null || leftRowCenterDists.Length < 3) leftRowCenterDists = new float[] { 15f, 30f, 45f };
        if (rightRowCenterDists == null || rightRowCenterDists.Length < 3) rightRowCenterDists = new float[] { 15f, 30f, 45f };

        // Cleanup self renderers
        if (GetComponent<MeshRenderer>()) Destroy(GetComponent<MeshRenderer>());
        if (GetComponent<MeshFilter>()) Destroy(GetComponent<MeshFilter>());
        
        mainVideoReceiver = FindObjectOfType<VideoReceiver>();
        
        SetupGhostReceiver();

        // Target Auto-Search
        if (playerTransform == null)
        {
            GameObject skel0 = GameObject.Find("Skeleton_0");
            if (skel0 != null) playerTransform = skel0.transform;
            else if (Camera.main != null) playerTransform = Camera.main.transform;
        }

        oscReceiver = FindObjectOfType<OSCReceiver>();
        if (oscReceiver != null)
        {
            oscReceiver.OnParamReceived += OnParamReceived;
            oscReceiver.OnStateReceived += OnStateReceived;
            oscReceiver.OnPoseReceived += OnPoseReceived;
        }

        // Initialize Cooldown Overlay (Hide by default)
        if (cooldownOverlay != null) cooldownOverlay.SetActive(false);

        SpawnGhosts();
    }
    
    void SetupGhostReceiver()
    {
        GameObject receiverObj = new GameObject("GhostVideoReceiver_5007");
        receiverObj.transform.SetParent(this.transform);
        
        ghostVideoReceiver = receiverObj.AddComponent<VideoReceiver>();
        ghostVideoReceiver.port = 5007; // Dedicated Port for Ghost Grid
        ghostVideoReceiver.transparency = 1.0f;
        
        SpriteRenderer sr = receiverObj.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    void OnDestroy()
    {
        if (oscReceiver != null) oscReceiver.OnParamReceived -= OnParamReceived;
        
        // Cleanup Buffer
        foreach (var tex in textureBuffer) Destroy(tex);
        textureBuffer.Clear();
        if (activeGhostTexture != null) Destroy(activeGhostTexture);
    }

    void UpdateLayout()
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            GhostUnit unit = ghosts[i];
            if (unit == null) continue;

            // Layout
            bool isLeft = (i >= 8);
            int localIdx = i % 8;
            int row = (localIdx < 3) ? 0 : (localIdx < 6) ? 1 : 2; 
            int colInRow = (row == 0) ? localIdx : (row == 1) ? localIdx - 3 : localIdx - 6;

            float centerX = isLeft ? leftRowCenterDists[row] : rightRowCenterDists[row];
            float gap = isLeft ? leftRowGaps[row] : rightRowGaps[row];
            
            // Convert to World Units
            float worldCenterX = ConvertUserToWorldX(centerX);
            float worldGap = ConvertUserToWorldX(gap);

            // Center Layout Logic:
            float centerIndex = (row == 2) ? 0.5f : 1.0f; 
            float offsetFromCenter = colInRow - centerIndex;
            
            // Linear Layout Logic
            float rawX = worldCenterX + (offsetFromCenter * worldGap);
            float finalX = rawX * (isLeft ? -1f : 1f);
            
            float sideShift = isLeft ? -ConvertUserToWorldX(teamGapX) : ConvertUserToWorldX(teamGapX);
            finalX += sideShift;

            float finalY = ConvertUserToWorldY(isLeft ? leftRowHeights[row] : rightRowHeights[row]);
            float finalZ = 0f;

            // Apply Position
            Vector3 basePos = new Vector3(finalX, finalY, finalZ);
            unit.SetBasePosition(basePos);
            
            // Apply Scale
            float s = isLeft ? leftRowScales[row] : rightRowScales[row];
            float finalScale = baseGhostSize * ghostScaleMultiplier * s;
            unit.transform.localScale = Vector3.one * finalScale;
            
            unit.SetTilt(0f);
            
            unit.gameObject.SetActive(basePos.magnitude > 0.1f);
        }
    }

    void SpawnGhosts()
    {
        foreach (var g in ghosts) if (g != null) Destroy(g.gameObject);
        ghosts.Clear();

        if (ghostMaterial == null)
        {
            Shader s = Shader.Find("Custom/GhostTransparent");
            if (s != null) ghostMaterial = new Material(s);
        }

        for (int i = 0; i < totalGhosts; i++)
        {
            CreateGhost(i);
        }
    }

    void CreateGhost(int index)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = $"GhostUnit_{index}";
        obj.transform.SetParent(this.transform);
        Destroy(obj.GetComponent<Collider>());
        
        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        if (ghostMaterial != null) mr.sharedMaterial = ghostMaterial;
        
        GhostUnit unit = obj.AddComponent<GhostUnit>();
        unit.Initialize(playerTransform);
        
        // 16-Color Palette
        float[] fixedHues = new float[] { 
            // Right Row 0 (Front)
            0.0f,    // 0: Red
            0.33f,   // 1: Pure Green
            0.66f,   // 2: Blue
            // Right Row 1 (Mid)
            0.15f,   // 3: Yellow
            0.5f,    // 4: Cyan
            0.83f,   // 5: Magenta
            // Right Row 2 (Back)
            0.26f,   // 6: Lime Green
            0.75f,   // 7: Purple
            
            // Left Row 0 (Front)
            0.08f,   // 8: Orange
            0.30f,   // 9: Bright Green
            0.38f,   // 10: Mint Green
            // Left Row 1 (Mid)
            0.92f,   // 11: Pink
            0.58f,   // 12: Sky Blue
            0.22f,   // 13: Yellow Green
            // Left Row 2 (Back)
            0.20f,   // 14: Chartreuse
            0.80f    // 15: Violet
        };
        
        int paletteIndex = index % fixedHues.Length;
        float baseHue = fixedHues[paletteIndex];
        
        // Small noise for individuality
        float noise = ((index * 13.0f) % 1.0f) * 0.05f - 0.025f; 
        float hue = baseHue + noise;
        if (hue < 0f) hue += 1.0f;
        if (hue > 1f) hue -= 1.0f;

        Color c = Color.HSVToRGB(hue, 0.9f, 1.0f);
        
        // 色を保存
        if (index < ghostColors.Length)
        {
            ghostColors[index] = c;
        }
        
        // enableSummoningがtrueの場合は初期Alpha=0、falseの場合は1
        if (enableSummoning)
        {
            c.a = 0f;
        }
        else
        {
            c.a = 1f;
        }
        
        unit.SetColor(c);
        
        int cols = 4;
        int rows = 4;
        float scaleX = 1.0f / cols;
        float scaleY = 1.0f / rows;
        
        unit.SetUV(new Vector2((index % cols) * scaleX, 1.0f - (index / cols + 1) * scaleY), new Vector2(scaleX, scaleY));
        
        unit.SetSwayParams(swaySpeed, swayAmount, index * 12.34f);
        
        ghosts.Add(unit);
    }
    
    // Coordinate Conversion Helpers
    float ConvertUserToWorldX(float userX)
    {
        if (Camera.main == null) return userX; // Fallback
        
        float camHeight = Camera.main.orthographicSize * 2.0f;
        float camWidth = camHeight * Camera.main.aspect;
        
        // User Def: 0 = Center, 100 = Screen Edge (Half Width)
        float halfWidth = camWidth * 0.5f;
        return userX * (halfWidth / 100.0f);
    }

    float ConvertUserToWorldY(float userY)
    {
        if (Camera.main == null) return userY;

        // User Def: 0 = Center, 50 = Screen Top (Half Height)
        float halfHeight = Camera.main.orthographicSize;
        return userY * (halfHeight / 50.0f);
    }

    void OnParamReceived(string name, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;

        if (name == "ghostScaleMultiplier")
        {
            ghostScaleMultiplier = Mathf.Clamp(value, 0.2f, 10.0f);
            return;
        }
        else if (name == "baseGhostSize") 
        {
            baseGhostSize = Mathf.Clamp(value, 0.2f, 10.0f);
            return;
        }

        if (name == "teamGapX") teamGapX = value;
        else if (name == "swayAmount") swayAmount = value; 
        else if (name == "swaySpeed") swaySpeed = value;
        else if (name == "cooldownOverlay")
        {
            if (cooldownOverlay != null) cooldownOverlay.SetActive(value > 0.5f);
        }
        else if (name.StartsWith("L_") || name.StartsWith("R_")) ParseGranularParam(name, value);
        else if (name.StartsWith("leftRow") || name.StartsWith("rightRow")) ParseLongParam(name, value);
    }

    void ParseGranularParam(string name, float value)
    {
        string[] parts = name.Split('_');
        if (parts.Length < 3) return;

        bool isLeft = (parts[0] == "L");
        int r_val;
        if (!int.TryParse(parts[1], out r_val)) return;
        int row = r_val;
        
        string type = parts[2]; 

        if (row < 0 || row > 2) return;

        if (isLeft)
        {
            if (type == "H") leftRowHeights[row] = value;
            else if (type == "S") leftRowScales[row] = Mathf.Clamp(value, 0.2f, 10.0f); 
            else if (type == "G") leftRowGaps[row] = value;
            else if (type == "D") leftRowCenterDists[row] = value;
        }
        else
        {
            if (type == "H") rightRowHeights[row] = value;
            else if (type == "S") rightRowScales[row] = Mathf.Clamp(value, 0.2f, 10.0f);
            else if (type == "G") rightRowGaps[row] = value;
            else if (type == "D") rightRowCenterDists[row] = value;
        }
    }

    void ParseLongParam(string name, float value)
    {
        string[] parts = name.Split('_');
        if (parts.Length < 2) return;
        
        string prefix = parts[0]; 
        string type = parts[1];   

        bool isLeft = prefix.StartsWith("left");
        string rowStr = prefix.Substring(isLeft ? 7 : 8); 
        
        int row;
        if (!int.TryParse(rowStr, out row)) return;
        if (row < 0 || row > 2) return;

        if (isLeft)
        {
            if (type == "height") leftRowHeights[row] = value;
            else if (type == "scale") leftRowScales[row] = Mathf.Clamp(value, 0.2f, 10.0f);
            else if (type == "gap") leftRowGaps[row] = value;
            else if (type == "dist") leftRowCenterDists[row] = value;
        }
        else
        {
            if (type == "height") rightRowHeights[row] = value;
            else if (type == "scale") rightRowScales[row] = Mathf.Clamp(value, 0.2f, 10.0f);
            else if (type == "gap") rightRowGaps[row] = value;
            else if (type == "dist") rightRowCenterDists[row] = value;
        }
    }

    void Update()
    {
        Texture currentTex = null;
        if (ghostVideoReceiver != null)
        {
            currentTex = ghostVideoReceiver.texture;
            
            if (ghostVideoReceiver.targetSpriteRenderer != null && ghostVideoReceiver.targetSpriteRenderer.enabled)
            {
                ghostVideoReceiver.targetSpriteRenderer.enabled = false;
            }
        }

        if (currentTex != null && currentTex.width <= 16)
        {
            currentTex = null; 
        }

        if (currentTex != null)
        {
            if (currentTex.wrapMode != TextureWrapMode.Clamp) currentTex.wrapMode = TextureWrapMode.Clamp;
            
            RenderTexture rt = new RenderTexture(currentTex.width, currentTex.height, 0);
            Graphics.Blit(currentTex, rt);
            
            textureBuffer.Enqueue(rt);

            if (textureBuffer.Count > ghostDelayFrames)
            {
                RenderTexture pastTex = textureBuffer.Dequeue();
                
                foreach (var unit in ghosts)
                {
                    unit.SetTexture(pastTex);
                }
                
                if (activeGhostTexture != null) Destroy(activeGhostTexture);
                activeGhostTexture = pastTex;
            }
        }

        UpdateLayout();
        
        // 3. Summoning Update (Ver 5.0)
        if (enableSummoning)
        {
            UpdateSummoning();
        }
    }

    void UpdateSummoning()
    {
        if (currentPhase != "POSSESSED" && currentPhase != "COOLDOWN") 
        {
            // IDLE時は全員透明
            if (currentPhase == "IDLE")
            {
                for (int i = 0; i < ghosts.Count; i++)
                {
                    SetGhostAlpha(i, 0f);
                }
            }
            return;
        }

        float elapsed = Time.time - summonStartTime;

        for (int i = 0; i < ghosts.Count && i < ghostSummonDelays.Length; i++)
        {
            float delay = ghostSummonDelays[i];
            float startTime = delay;
            float endTime = delay + summonFadeDuration;

            float alpha = 0f;
            
            if (elapsed < startTime)
            {
                alpha = 0f;
            }
            else if (elapsed >= endTime)
            {
                alpha = 1.0f;
            }
            else
            {
                // フェードイン中
                alpha = (elapsed - startTime) / summonFadeDuration;
            }

            SetGhostAlpha(i, alpha);
        }
    }

    void SetGhostAlpha(int index, float alpha)
    {
        if (index < 0 || index >= ghosts.Count || ghosts[index] == null) return;

        MeshRenderer renderer = ghosts[index].GetComponent<MeshRenderer>();
        if (renderer == null) return;

        // 保存された基本色を取得
        Color baseColor = (index < ghostColors.Length) ? ghostColors[index] : Color.white;
        baseColor.a = alpha;

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propBlock);
        propBlock.SetColor("_TintColor", baseColor);
        renderer.SetPropertyBlock(propBlock);
    }

    int GetRowIndex(int ghostIndex)
    {
        int localIdx = ghostIndex % 8;
        return (localIdx < 3) ? 0 : (localIdx < 6) ? 1 : 2;
    }

    void OnStateReceived(string state, float progress)
    {
        if (currentPhase != state)
        {
            currentPhase = state;
            Debug.Log($"[GhostLayoutManager] Phase changed to: {state}");

            if (enableSummoning)
            {
                if (state == "POSSESSED")
                {
                    summonStartTime = Time.time;
                    Debug.Log($"[GhostLayoutManager] Summoning started at {summonStartTime}");
                }
                else if (state == "IDLE")
                {
                    // 全員を透明に
                    for (int i = 0; i < ghosts.Count; i++)
                    {
                        SetGhostAlpha(i, 0f);
                    }
                    Debug.Log("[GhostLayoutManager] All ghosts set to transparent (IDLE)");
                }
            }
        }
    }

    void OnPoseReceived(int id, float energy, List<Vector4> landmarks)
    {
        if (id == 0) // Player's energy
        {
            currentEnergy = energy;
            // TODO: GhostUnitにEnergy連動機能を追加する場合はここでブロードキャスト
        }
    }


}
