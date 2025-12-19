using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ver 5.0: 亡霊システムの司令塔。
/// 全体ステート管理、GhostVisual生成・管理、OSC受信とブロードキャストを担当。
/// </summary>
public class GhostManager : MonoBehaviour
{
    [Header("References")]
    public Transform playerTransform;
    
    private VideoReceiver ghostVideoReceiver;

    [Header("Ghost Settings")]
    public Material ghostMaterial;
    public float baseGhostSize = 2.0f;
    public float ghostScaleMultiplier = 1.0f;

    [Header("Texture Buffering")]
    [Range(0, 120)] public int ghostDelayFrames = 5;
    private Queue<RenderTexture> textureBuffer = new Queue<RenderTexture>();
    private RenderTexture activeGhostTexture;

    [Header("Layout Parameters")]
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
    private List<GhostVisual> ghosts = new List<GhostVisual>();

    [Header("Animation")]
    public float swayAmount = 0.5f;
    public float swaySpeed = 1.0f;
    public float teamGapX = 0.0f;

    [Header("Summoning (Ver 5.0)")]
    public bool enableSummoning = false;
    private string currentPhase = "IDLE";
    private float summonStartTime = 0f;
    public float summonDurationPerRow = 1.0f; // 1列あたりの召喚時間（秒）

    [Header("UI Overlay")]
    public GameObject cooldownOverlay;

    private OSCReceiver oscReceiver;
    private float currentEnergy = 0f; // OSCから受信したEnergy値

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

        if (cooldownOverlay != null) cooldownOverlay.SetActive(false);

        SpawnGhosts();
    }

    void SetupGhostReceiver()
    {
        GameObject receiverObj = new GameObject("GhostVideoReceiver_5007");
        receiverObj.transform.SetParent(this.transform);

        ghostVideoReceiver = receiverObj.AddComponent<VideoReceiver>();
        ghostVideoReceiver.port = 5007;
        ghostVideoReceiver.transparency = 1.0f;

        SpriteRenderer sr = receiverObj.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    void OnDestroy()
    {
        if (oscReceiver != null)
        {
            oscReceiver.OnParamReceived -= OnParamReceived;
            oscReceiver.OnStateReceived -= OnStateReceived;
            oscReceiver.OnPoseReceived -= OnPoseReceived;
        }

        foreach (var tex in textureBuffer) Destroy(tex);
        textureBuffer.Clear();
        if (activeGhostTexture != null) Destroy(activeGhostTexture);
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
        obj.name = $"Ghost_{index}";
        obj.transform.SetParent(this.transform);
        Destroy(obj.GetComponent<Collider>());

        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        if (ghostMaterial != null) mr.sharedMaterial = ghostMaterial;

        GhostVisual visual = obj.AddComponent<GhostVisual>();
        visual.Initialize(playerTransform, index);

        // 16-Color Palette
        float[] fixedHues = new float[] {
            0.0f, 0.33f, 0.66f, 0.15f, 0.5f, 0.83f, 0.26f, 0.75f,
            0.08f, 0.30f, 0.38f, 0.92f, 0.58f, 0.22f, 0.20f, 0.80f
        };

        int paletteIndex = index % fixedHues.Length;
        float baseHue = fixedHues[paletteIndex];
        float noise = ((index * 13.0f) % 1.0f) * 0.05f - 0.025f;
        float hue = Mathf.Repeat(baseHue + noise, 1.0f);

        Color c = Color.HSVToRGB(hue, 0.9f, 1.0f);
        visual.SetColor(c);

        // UV Mapping (4x4 Grid)
        int cols = 4, rows = 4;
        float scaleX = 1.0f / cols, scaleY = 1.0f / rows;
        visual.SetUV(new Vector2((index % cols) * scaleX, 1.0f - (index / cols + 1) * scaleY), new Vector2(scaleX, scaleY));

        visual.SetSwayParams(swaySpeed, swayAmount, index * 12.34f);

        // 初期Alpha = 0 (召喚演出用)
        if (enableSummoning)
        {
            visual.SetSummonAlpha(0f);
        }

        ghosts.Add(visual);
    }

    void Update()
    {
        // 1. Texture Handling
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
                foreach (var visual in ghosts)
                {
                    visual.SetTexture(pastTex);
                }
                if (activeGhostTexture != null) Destroy(activeGhostTexture);
                activeGhostTexture = pastTex;
            }
        }

        // 2. Layout Update
        UpdateLayout();

        // 3. Summoning Update
        if (enableSummoning)
        {
            UpdateSummoning();
        }

        // 4. Energy Broadcast
        BroadcastEnergy(currentEnergy);
    }

    void UpdateLayout()
    {
        for (int i = 0; i < ghosts.Count; i++)
        {
            GhostVisual visual = ghosts[i];
            if (visual == null) continue;

            bool isLeft = (i >= 8);
            int localIdx = i % 8;
            int row = (localIdx < 3) ? 0 : (localIdx < 6) ? 1 : 2;
            int colInRow = (row == 0) ? localIdx : (row == 1) ? localIdx - 3 : localIdx - 6;

            float centerX = isLeft ? leftRowCenterDists[row] : rightRowCenterDists[row];
            float gap = isLeft ? leftRowGaps[row] : rightRowGaps[row];

            float worldCenterX = ConvertUserToWorldX(centerX);
            float worldGap = ConvertUserToWorldX(gap);

            float centerIndex = (row == 2) ? 0.5f : 1.0f;
            float offsetFromCenter = colInRow - centerIndex;

            float rawX = worldCenterX + (offsetFromCenter * worldGap);
            float finalX = rawX * (isLeft ? -1f : 1f);
            float sideShift = isLeft ? -ConvertUserToWorldX(teamGapX) : ConvertUserToWorldX(teamGapX);
            finalX += sideShift;

            float finalY = ConvertUserToWorldY(isLeft ? leftRowHeights[row] : rightRowHeights[row]);

            Vector3 basePos = new Vector3(finalX, finalY, 0f);
            visual.SetBasePosition(basePos);

            float s = isLeft ? leftRowScales[row] : rightRowScales[row];
            float finalScale = baseGhostSize * ghostScaleMultiplier * s;
            visual.transform.localScale = Vector3.one * finalScale;

            visual.SetTilt(0f);
            visual.gameObject.SetActive(basePos.magnitude > 0.1f);
        }
    }

    void UpdateSummoning()
    {
        if (currentPhase != "POSSESSED" && currentPhase != "COOLDOWN") return;

        float elapsed = Time.time - summonStartTime;

        for (int i = 0; i < ghosts.Count; i++)
        {
            int row = GetRowIndex(i);
            float startTime = row * summonDurationPerRow;
            float endTime = startTime + summonDurationPerRow;

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
                alpha = (elapsed - startTime) / summonDurationPerRow;
            }

            ghosts[i].SetSummonAlpha(alpha);
        }
    }

    int GetRowIndex(int ghostIndex)
    {
        int localIdx = ghostIndex % 8;
        return (localIdx < 3) ? 0 : (localIdx < 6) ? 1 : 2;
    }

    void BroadcastEnergy(float energy)
    {
        foreach (var visual in ghosts)
        {
            if (visual != null) visual.SetEnergyLevel(energy);
        }
    }

    float ConvertUserToWorldX(float userX)
    {
        if (Camera.main == null) return userX;
        float camHeight = Camera.main.orthographicSize * 2.0f;
        float camWidth = camHeight * Camera.main.aspect;
        float halfWidth = camWidth * 0.5f;
        return userX * (halfWidth / 100.0f);
    }

    float ConvertUserToWorldY(float userY)
    {
        if (Camera.main == null) return userY;
        float halfHeight = Camera.main.orthographicSize;
        return userY * (halfHeight / 50.0f);
    }

    void OnParamReceived(string name, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;

        if (name == "ghostScaleMultiplier") ghostScaleMultiplier = Mathf.Clamp(value, 0.2f, 10.0f);
        else if (name == "baseGhostSize") baseGhostSize = Mathf.Clamp(value, 0.2f, 10.0f);
        else if (name == "teamGapX") teamGapX = value;
        else if (name == "swayAmount") swayAmount = value;
        else if (name == "swaySpeed") swaySpeed = value;
        else if (name == "cooldownOverlay")
        {
            if (cooldownOverlay != null) cooldownOverlay.SetActive(value > 0.5f);
        }
        else if (name.StartsWith("L_") || name.StartsWith("R_")) ParseGranularParam(name, value);
        else if (name.StartsWith("leftRow") || name.StartsWith("rightRow")) ParseLongParam(name, value);
    }

    void OnStateReceived(string state, float progress)
    {
        if (currentPhase != state)
        {
            currentPhase = state;

            if (enableSummoning)
            {
                if (state == "POSSESSED")
                {
                    summonStartTime = Time.time;
                }
                else if (state == "IDLE")
                {
                    // 全員を透明に
                    foreach (var visual in ghosts)
                    {
                        if (visual != null) visual.SetSummonAlpha(0f);
                    }
                }
            }
        }
    }

    void OnPoseReceived(int id, float energy, List<Vector4> landmarks)
    {
        if (id == 0) // Player's energy
        {
            currentEnergy = energy;
        }
    }

    void ParseGranularParam(string name, float value)
    {
        string[] parts = name.Split('_');
        if (parts.Length < 3) return;

        bool isLeft = (parts[0] == "L");
        if (!int.TryParse(parts[1], out int row)) return;
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

        if (!int.TryParse(rowStr, out int row)) return;
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
}
