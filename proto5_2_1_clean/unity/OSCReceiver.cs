using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System;
using System.Threading;

/// <summary>
/// PythonからのOSCメッセージを受信するクラス。
/// VideoReceiverと同様のThread方式に変更。
/// </summary>
public class OSCReceiver : MonoBehaviour
{
    public int port = 5005; // 受信ポート番号
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = true;
    
    private object lockObject = new object(); // 排他制御用
    private Queue<OSCMessage> messageQueue = new Queue<OSCMessage>(); // メッセージキュー

    // 骨格データ受信イベント (Action型に変更)
    public event Action<int, float, List<Vector4>> OnPoseReceived;

    // ステート受信イベント (Action型に変更)
    public event Action<string, float> OnStateReceived;

    // パラメータ受信イベント (Action型に変更)
    public event Action<string, float> OnParamReceived;

    void Start()
    {
        // UDPクライアントの開始
        try 
        {
            udpClient = new UdpClient(port);
            udpClient.Client.ReceiveBufferSize = 1024 * 64;
            
            receiveThread = new Thread(new ThreadStart(ReceiveLoop));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            
            Debug.Log($"OSC Receiver started on port {port} (Thread Mode)");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start OSC Receiver: {e.Message}");
        }
    }

    // 受信ループ（別スレッド）
    void ReceiveLoop()
    {
        IPEndPoint ip = new IPEndPoint(IPAddress.Any, port);
        
        while (isRunning)
        {
            try
            {
                if (udpClient.Available > 0)
                {
                    byte[] bytes = udpClient.Receive(ref ip);
                    
                    // バイト列をパース
                    OSCMessage msg = ParseOSC(bytes);
                    lock (lockObject)
                    {
                        messageQueue.Enqueue(msg);
                    }
                }
                else
                {
                    Thread.Sleep(1); // CPU負荷軽減
                }
            }
            catch (Exception)
            {
                // Thread内でのLogはメインスレッド負荷になるので控える
                if (!isRunning) break;
            }
        }
    }

    // メインスレッドでの更新処理
    void Update()
    {
        lock (lockObject)
        {
            while (messageQueue.Count > 0)
            {
                OSCMessage msg = messageQueue.Dequeue();
                ProcessMessage(msg);
            }
        }
    }

    // メッセージ内容に応じた処理の振り分け
    void ProcessMessage(OSCMessage msg)
    {
        if (msg.address == "/pose")
        {
            // フォーマット: [id, energy, x0, y0, z0, v0, ...]
            if (msg.values.Count >= 2)
            {
                int id = (int)msg.values[0];
                float energy = (float)msg.values[1];
                List<Vector4> landmarks = new List<Vector4>();
                
                // 座標データの読み出し (x, y, z, visibility)
                for (int i = 2; i < msg.values.Count; i += 4)
                {
                    if (i + 3 < msg.values.Count)
                    {
                        float x = (float)msg.values[i];
                        float y = (float)msg.values[i+1];
                        float z = (float)msg.values[i+2];
                        float v = (float)msg.values[i+3];
                        landmarks.Add(new Vector4(x, y, z, v));
                    }
                }
                // イベント発火
                OnPoseReceived?.Invoke(id, energy, landmarks);
            }
        }
        else if (msg.address == "/state")
        {
            // フォーマット: [state_name, progress]
            if (msg.values.Count >= 2)
            {
                string state = (string)msg.values[0];
                float progress = (float)msg.values[1];
                OnStateReceived?.Invoke(state, progress);
            }
        }
        else if (msg.address == "/param")
        {
            // Legacy Format: [param_name, value]
            if (msg.values.Count >= 2)
            {
                string paramName = (string)msg.values[0];
                float value = (float)msg.values[1];
                OnParamReceived?.Invoke(paramName, value);
            }
        }
    }

    // --- 簡易OSCパーサー ---
    struct OSCMessage
    {
        public string address;
        public List<object> values;
    }

    // バイト列からOSCメッセージを復元する処理
    OSCMessage ParseOSC(byte[] bytes)
    {
        OSCMessage msg = new OSCMessage();
        msg.values = new List<object>();
        
        int index = 0;
        
        // 1. アドレスの読み込み
        msg.address = ReadString(bytes, ref index);
        
        // 2. 型タグの読み込み
        string typeTags = ReadString(bytes, ref index);
        
        // 3. 引数の読み込み
        foreach (char type in typeTags)
        {
            if (type == ',') continue;
            
            if (type == 'i') // int32
            {
                msg.values.Add(ReadInt(bytes, ref index));
            }
            else if (type == 'f') // float32
            {
                msg.values.Add(ReadFloat(bytes, ref index));
            }
            else if (type == 's') // string
            {
                msg.values.Add(ReadString(bytes, ref index));
            }
        }
        
        return msg;
    }

    // 文字列読み込み（4バイトアライメント対応）
    string ReadString(byte[] bytes, ref int index)
    {
        int start = index;
        while (index < bytes.Length && bytes[index] != 0) index++;
        string s = Encoding.ASCII.GetString(bytes, start, index - start);
        index++; // null文字スキップ
        // 4バイト境界に合わせる
        while (index % 4 != 0) index++;
        return s;
    }

    // 整数読み込み（ビッグエンディアン対応）
    int ReadInt(byte[] bytes, ref int index)
    {
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes, index, 4);
        int v = BitConverter.ToInt32(bytes, index);
        index += 4;
        return v;
    }

    // 浮動小数点数読み込み
    float ReadFloat(byte[] bytes, ref int index)
    {
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes, index, 4);
        float v = BitConverter.ToSingle(bytes, index);
        index += 4;
        return v;
    }

    void OnDestroy()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
    }
}
