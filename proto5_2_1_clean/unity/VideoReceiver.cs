using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class VideoReceiver : MonoBehaviour
{
    public int port = 5006;
    // public Renderer targetRenderer; // Deprecated: Using SpriteRenderer now
    public SpriteRenderer targetSpriteRenderer; // Assign this or it will be found/added
    public Material targetMaterial; // (Optional)
    [Range(0f, 1f)] public float transparency = 0.5f; // シルエットの透明度
    
    // Expose current sprite for Ghosts
    public Sprite currentSprite => targetSpriteRenderer != null ? targetSpriteRenderer.sprite : null;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = true;
    
    private byte[] receivedData = null;
    private bool hasNewData = false;
    private object lockObject = new object();
    
    public Texture2D texture; // Public for RealtimeViewer
    private Rect lastRect;

    void Start()
    {
        udpClient = new UdpClient(port);
        udpClient.Client.ReceiveBufferSize = 1024 * 1024; // 1MB buffer
        
        // Disable mipmaps for performance and compatibility with CopyTexture
        texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        
        // 【重要】初期状態は「透明」で塗りつぶす (白飛び防止)
        Color32[] resetColors = new Color32[4];
        for(int i=0; i<4; i++) resetColors[i] = new Color32(0,0,0,0);
        texture.SetPixels32(resetColors);
        texture.Apply();
        
        // SpriteRendererのセットアップ
        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponent<SpriteRenderer>();
            if (targetSpriteRenderer == null)
            {
                // 競合するMeshRenderer/MeshFilterがあれば削除
                var meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer != null) DestroyImmediate(meshRenderer);
                
                var meshFilter = GetComponent<MeshFilter>();
                if (meshFilter != null) DestroyImmediate(meshFilter);
                
                // SpriteRendererを追加
                targetSpriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        // シェーダー設定 (黒を透過させるためAdditiveにする)
        Shader additiveShader = Shader.Find("SimpleAdditive"); // ユーザーが作った方があれば優先
        if (additiveShader == null) additiveShader = Shader.Find("Mobile/Particles/Additive"); // フォールバック
        
        if (additiveShader != null)
        {
            targetSpriteRenderer.material = new Material(additiveShader);
        }
        
        // マスク設定: マスクの外側だけ表示 (スポットライトの中は非表示)
        targetSpriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
        targetSpriteRenderer.sortingOrder = 0; // Middle

        // 初期スプライト作成
        UpdateSprite();
        
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void UpdateSprite()
    {
        if (targetSpriteRenderer == null || texture == null) return;
        
        // テクスチャサイズが変わった場合のみスプライトを作り直すのが理想だが、
        // LoadImageでサイズが変わる可能性があるため、Rectチェック
        Rect newRect = new Rect(0, 0, texture.width, texture.height);
        if (newRect != lastRect)
        {
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            targetSpriteRenderer.sprite = Sprite.Create(texture, newRect, pivot);
            lastRect = newRect;
            
            // カメラの高さに合わせてスケール調整
            if (Camera.main != null)
            {
                float camHeight = Camera.main.orthographicSize * 2.0f;
                float spriteHeight = texture.height / 100.0f; // Default PPU is 100
                float scale = camHeight / spriteHeight;
                transform.localScale = new Vector3(scale, scale, 1.0f);
            }
        }
    }
    
    void Update()
    {
        if (hasNewData)
        {
            lock (lockObject)
            {
                if (receivedData != null)
                {
                    bool loaded = texture.LoadImage(receivedData);
                    if (loaded)
                    {
                        UpdateSprite();
                    }
                    hasNewData = false;
                }
            }
        }
        
        // Transparency Update
        if (targetSpriteRenderer != null)
        {
            Color c = targetSpriteRenderer.color;
            if (c.a != transparency)
            {
                c.a = transparency;
                targetSpriteRenderer.color = c;
            }
        }
    }

    void ReceiveData()
    {
        IPEndPoint ip = new IPEndPoint(IPAddress.Any, port);
        byte[] frameBuffer = new byte[0];
        int totalPackets = 0;
        int receivedPackets = 0;
        int frameIndex = -1;

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref ip);
                
                // Header check: needs at least 2 bytes (index, total)
                if (data.Length < 2) continue;

                int packetIndex = data[0];
                int packetCount = data[1];
                
                // New frame start
                if (packetIndex == 0)
                {
                    frameBuffer = new byte[0];
                    totalPackets = packetCount;
                    receivedPackets = 0;
                    frameIndex++; 
                }

                // Append data (skip 2 byte header)
                if (packetIndex < packetCount) // Basic safety
                {
                    int currentLen = frameBuffer.Length;
                    int chunkLen = data.Length - 2;
                    if (chunkLen > 0)
                    {
                        System.Array.Resize(ref frameBuffer, currentLen + chunkLen);
                        System.Array.Copy(data, 2, frameBuffer, currentLen, chunkLen);
                        receivedPackets++;
                    }
                }

                // Check completeness
                if (receivedPackets >= totalPackets && totalPackets > 0)
                {
                    lock (lockObject)
                    {
                        receivedData = frameBuffer;
                        hasNewData = true;
                    }
                }
            }
            catch (Exception)
            {
                // Socket closed or thread aborted
            }
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
        if (udpClient != null) udpClient.Close();
    }
}
