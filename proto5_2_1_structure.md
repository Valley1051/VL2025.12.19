# Proto 5.2.1 システムアーキテクチャ

---

## 🏗️ 全体アーキテクチャ

```mermaid
flowchart TB
    subgraph Hardware["ハードウェア層"]
        CAM[🎥 カメラ]
    end

    subgraph Python["Python Backend"]
        direction TB
        PE[ポーズ推定<br/>MediaPipe/YOLO]
        VIS[Visualizer<br/>映像生成]
        GM[GhostManager<br/>ループ録画]
        OSC_S[OSCManager<br/>データ送信]
        VS[VideoSender<br/>映像送信]
    end

    subgraph Network["ネットワーク層"]
        UDP1[UDP:5005<br/>OSC]
        UDP2[UDP:5006<br/>Live Mask]
        UDP3[UDP:5007<br/>Ghost Grid]
    end

    subgraph Unity["Unity Frontend"]
        direction TB
        OSC_R[OSCReceiver]
        VR1[VideoReceiver #1]
        VR2[VideoReceiver #2]
        
        subgraph Display["表示系"]
            SK[SkeletonController<br/>骸骨描画]
            GL[GhostLayoutManager<br/>亡霊配置]
            SP[SpotlightController<br/>マスク制御]
            DM[DustMotes<br/>塵パーティクル]
            MU[MusicManager<br/>音楽]
        end
    end

    subgraph Output["出力"]
        SCREEN[🖥️ 画面表示]
        AUDIO[🔊 音声出力]
    end

    CAM --> PE
    PE --> VIS
    PE --> GM
    VIS --> VS
    GM --> VIS
    PE --> OSC_S
    
    OSC_S --> UDP1
    VS --> UDP2
    VS --> UDP3
    
    UDP1 --> OSC_R
    UDP2 --> VR1
    UDP3 --> VR2
    
    OSC_R --> SK
    OSC_R --> GL
    OSC_R --> SP
    OSC_R --> MU
    VR1 --> SK
    VR2 --> GL
    
    SK --> SCREEN
    GL --> SCREEN
    SP --> SCREEN
    DM --> SCREEN
    MU --> AUDIO
```

---

## 📡 通信プロトコル

```mermaid
sequenceDiagram
    participant P as Python
    participant U as Unity

    rect rgb(70, 130, 180)
        Note over P,U: OSC通信 (UDP:5005)
        P->>U: /pose [id, energy, x0,y0,z0,v0, ...]
        P->>U: /state [phase_name, progress]
        P->>U: /param [param_name, value]
    end

    rect rgb(60, 179, 113)
        Note over P,U: ビデオストリーム (UDP:5006/5007)
        P->>U: Live Mask (体験者シルエット)
        P->>U: Ghost Grid (4x4 ゴースト映像)
    end
```

---

## 📦 レイヤーアーキテクチャ

```mermaid
graph TB
    subgraph L1["入力層"]
        I1[カメラ入力]
        I2[OSCコマンド]
    end

    subgraph L2["処理層 (Python)"]
        P1[ポーズ推定]
        P2[セグメンテーション]
        P3[ゴースト管理]
        P4[映像合成]
    end

    subgraph L3["通信層"]
        C1[OSC (骨格/状態/パラメータ)]
        C2[Video (マスク/グリッド)]
    end

    subgraph L4["表示層 (Unity)"]
        U1[骸骨レンダリング]
        U2[ゴーストレンダリング]
        U3[エフェクト<br/>スポットライト/塵]
        U4[音楽制御]
    end

    subgraph L5["出力層"]
        O1[画面出力]
        O2[音声出力]
    end

    I1 --> P1
    I1 --> P2
    I2 --> P4
    P1 --> P3
    P2 --> P4
    P3 --> P4
    P1 --> C1
    P4 --> C2
    C1 --> U1
    C1 --> U4
    C2 --> U1
    C2 --> U2
    U1 --> O1
    U2 --> O1
    U3 --> O1
    U4 --> O2
```

---

## 🎭 ゲームフェーズ状態遷移

```mermaid
stateDiagram-v2
    [*] --> IDLE: 起動
    
    IDLE --> POSSESSED: 人物検出
    POSSESSED --> COOLDOWN: タイムアウト
    COOLDOWN --> IDLE: 完了
    COOLDOWN --> POSSESSED: 再検出
    
    note right of IDLE
        待機BGM再生
        ゴースト非表示
    end note
    
    note right of POSSESSED
        体験音楽再生
        ゴースト召喚演出
        スポットライトアニメーション
    end note
    
    note right of COOLDOWN
        ゴースト録画保存
        フェードアウト
    end note
```

---

## 🔗 Unityコンポーネント依存関係

```mermaid
graph LR
    subgraph Core["コア"]
        OSC[OSCReceiver]
        VR[VideoReceiver]
    end

    subgraph Visual["ビジュアル"]
        SK[SkeletonController]
        GL[GhostLayoutManager]
        GU[GhostUnit]
        GV[GhostVisual]
    end

    subgraph Effects["エフェクト"]
        SP[SpotlightController]
        DM[DustMotesController]
    end

    subgraph Audio["オーディオ"]
        MU[MusicManager]
    end

    OSC -->|OnPoseReceived| SK
    OSC -->|OnStateReceived| GL
    OSC -->|OnStateReceived| MU
    OSC -->|OnParamReceived| SP
    OSC -->|OnParamReceived| GL
    
    VR -->|currentSprite| SK
    VR -->|currentSprite| GL
    
    GL -->|管理| GU
    GU -->|表示| GV

    style OSC fill:#4a90d9
    style VR fill:#4a90d9
    style SK fill:#50c878
    style GL fill:#50c878
    style SP fill:#ffa500
    style MU fill:#da70d6
```

---

## 📊 データフロー詳細

```mermaid
flowchart LR
    subgraph Input
        CAM[カメラフレーム]
    end

    subgraph Pose["ポーズ推定"]
        MP[MediaPipe]
        YL[YOLO]
    end

    subgraph Data["データ生成"]
        LM[33 Landmarks<br/>x,y,z,visibility]
        MK[Segmentation<br/>Mask]
        EN[Energy<br/>運動量]
    end

    subgraph Output
        OSC_D["/pose データ"]
        VID_D["JPEG フレーム"]
    end

    CAM --> MP
    CAM --> YL
    MP --> LM
    MP --> MK
    YL --> LM
    YL --> MK
    LM --> EN
    LM --> OSC_D
    MK --> VID_D
    EN --> OSC_D
```

---

## 🎨 レンダリング階層

```mermaid
graph TB
    subgraph Layers["Sorting Order"]
        L0["0: Background"]
        L1["50: Silhouette (Live Mask)"]
        L2["100-199: Ghosts"]
        L3["200: Skeleton"]
        L4["500: Spotlight Mask"]
        L5["501: Spotlight Visual"]
        L6["600: Dust Particles"]
    end

    L0 --> L1 --> L2 --> L3 --> L4 --> L5 --> L6
```

---

## 📁 ファイル構成

```mermaid
graph TD
    subgraph Root["proto5_2_1_clean/"]
        SETUP[SETUP_GUIDE.md]
        
        subgraph PY["python/"]
            MAIN[main.py]
            VIS[visualizer.py]
            OSC[osc_manager.py]
            GM[ghost_manager.py]
            PE[pose_estimator*.py]
            VS[video_sender.py]
            DATA[ghost_loops.pkl]
        end
        
        subgraph UN["unity/"]
            CTRL[*Controller.cs]
            MGR[*Manager.cs]
            SHADER[*.shader]
        end
    end
```
