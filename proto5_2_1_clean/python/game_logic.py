import time

class GameLogic:
    """
    体験フローとステートを管理するクラス。
    【Ver 4.2】時間経過による自動COOLDOWN移行を追加。
    """
    def __init__(self, duration=68.0):  # 曲の長さ: 68秒
        self.duration = duration
        self.start_time = None
        self.is_running = False
        self.current_phase = "IDLE"
        
    def start_session(self):
        """
        セッションを開始します（体験者が現れた時）。
        """
        self.start_time = time.time()
        self.is_running = True
        self.current_phase = "POSSESSED"
        print(f"Session Started: POSSESSED (Duration: {self.duration}s)")

    def force_finish(self):
        """
        強制的にセッションを終了し、儀式（亡霊化）へ移行させます。
        その後、クールタイムに入ります。
        """
        if self.current_phase == "IDLE" or self.current_phase == "COOLDOWN":
            return

        print("The Ritual completes. Entering COOLDOWN...")
        self.current_phase = "COOLDOWN"
        self.start_time = time.time() # クールタイム計測開始

    def update(self):
        """
        毎フレーム呼び出され、経過時間を返します。
        【Ver 4.2】 時間経過による自動COOLDOWN移行を追加
        """
        if not self.is_running and self.current_phase != "COOLDOWN":
            return "IDLE", 0.0

        elapsed = time.time() - self.start_time
        
        # POSSESSED中に曲の長さ（duration）を超えたらCOOLDOWNへ自動移行
        if self.current_phase == "POSSESSED" and elapsed >= self.duration:
            print(f"Session time reached ({self.duration}s). Auto-transitioning to COOLDOWN...")
            self.force_finish()
            return self.current_phase, 0.0  # COOLDOWN開始時点でelapsed=0
        
        # COOLDOWN処理
        if self.current_phase == "COOLDOWN":
            cooldown_duration = 5.0 # 5秒間
            if elapsed > cooldown_duration:
                print("Cooldown finished. Session Reset.")
                self.reset()
                return "IDLE", 0.0
        
        return self.current_phase, elapsed

    def reset(self):
        """
        状態をリセットし、待機状態に戻ります。
        """
        self.is_running = False
        self.current_phase = "IDLE"
