from pythonosc import udp_client

class OSCManager:
    """
    UnityへOSC (Open Sound Control) メッセージを送信するクラス。
    UDPプロトコルを使用します。
    """
    def __init__(self, ip="127.0.0.1", port=5005):
        # 指定されたIPとポートでクライアントを作成
        self.client = udp_client.SimpleUDPClient(ip, port)

    def send_pose(self, landmarks, energy, player_id=0):
        """
        骨格座標と運動量をUnityへ送信します。
        
        Args:
            landmarks: ランドマークのリスト（各点はx, y, z, visibilityを持つ）
            energy: 運動量（float）
            player_id: プレイヤーID（0=現在の体験者, 1以降=過去の亡霊）
        """
        # 送信データの構築: [ID, Energy, x0, y0, z0, v0, x1, y1, z1, v1, ...]
        # python-oscはnumpy型を受け付けないため、明示的にキャストする
        data = [int(player_id), float(energy)]
        if landmarks:
            # MediaPipeのNormalizedLandmarkListは直接イテレートできない場合があるため .landmark を参照
            landmark_list = landmarks.landmark if hasattr(landmarks, "landmark") else landmarks
            for lm in landmark_list:
                # MediaPipeのランドマークオブジェクト、または辞書型に対応
                if hasattr(lm, 'x'):
                    data.extend([float(lm.x), float(lm.y), float(lm.z), float(lm.visibility)])
                elif isinstance(lm, dict):
                    data.extend([float(lm['x']), float(lm['y']), float(lm['z']), float(lm['v'])])
                elif isinstance(lm, (list, tuple)) and len(lm) >= 4:
                    data.extend([float(lm[0]), float(lm[1]), float(lm[2]), float(lm[3])])
                else:
                    # フォールバック
                    data.extend([0.0, 0.0, 0.0, 0.0])
        
        # "/pose" アドレスで送信
        self.client.send_message("/pose", data)

    def send_state(self, state_name, progress):
        """
        現在のゲームステート（進行状況）を送信します。
        
        Args:
            state_name: フェーズ名（"INTRO", "CHORUS" など）
            progress: 経過時間の割合（0.0 〜 1.0）など
        """
        self.client.send_message("/state", [state_name, float(progress)])

    def send_param(self, param_name, value):
        """
        汎用パラメータを送信します。
        
        Args:
            param_name: パラメータ名（"boneRotation", "ghostFadeSpeed" など）
            value: 値（float）
        """
        self.client.send_message("/param", [param_name, float(value)])
