import mediapipe as mp
import cv2
import numpy as np

class PoseEstimator:
    """
    MediaPipe Poseを使用して、Webカメラ映像から骨格を推定するクラス。
    """
    def __init__(self, min_detection_confidence=0.5, min_tracking_confidence=0.5):
        # MediaPipeのPoseソリューションを初期化
        self.mp_pose = mp.solutions.pose
        self.pose = self.mp_pose.Pose(
            min_detection_confidence=min_detection_confidence,
            min_tracking_confidence=min_tracking_confidence,
            model_complexity=1, # 0=軽量, 1=バランス, 2=高精度
            enable_segmentation=True # シルエット生成のためにセグメンテーションを有効化
        )
        self.mp_drawing = mp.solutions.drawing_utils

    def process_frame(self, frame):
        """
        BGR画像を処理し、骨格ランドマーク（座標データ）を返します。
        """
        # MediaPipeはRGB画像を必要とするため、BGRからRGBへ変換
        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        
        # パフォーマンス向上のため、書き込み不可に設定（参照渡しにする）
        image_rgb.flags.writeable = False
        
        # 推論実行
        results = self.pose.process(image_rgb)
        
        # 描画のために書き込み可能に戻す
        image_rgb.flags.writeable = True
        
        # 検出されたランドマークとセグメンテーションマスクを返す
        return results.pose_landmarks, results.segmentation_mask

    def calculate_energy(self, current_landmarks, prev_landmarks):
        """
        前フレームとの差分から「運動量（Energy）」を計算します。
        主要な関節（手首、足首、肩、腰）の移動距離の合計値です。
        """
        if not current_landmarks or not prev_landmarks:
            return 0.0
        
        energy = 0.0
        # 動きを検知するための主要関節インデックス
        # 11,12: 肩, 23,24: 腰, 15,16: 手首, 27,28: 足首
        indices = [11, 12, 15, 16, 23, 24, 27, 28]
        
        count = 0
        for i in indices:
            # インデックスが範囲内かチェック
            if i < len(current_landmarks.landmark) and i < len(prev_landmarks.landmark):
                c = current_landmarks.landmark[i] # 現在の座標
                p = prev_landmarks.landmark[i]    # 前フレームの座標
                
                # ユークリッド距離を計算 (3次元)
                dist = np.sqrt((c.x - p.x)**2 + (c.y - p.y)**2 + (c.z - p.z)**2)
                energy += dist
                count += 1
        
        # 合計値を返す（必要に応じて正規化しても良い）
        if count > 0:
            return energy
        return 0.0
