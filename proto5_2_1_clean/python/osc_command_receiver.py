from pythonosc import dispatcher, osc_server
import threading

class OSCCommandReceiver:
    """
    UnityからのOSCコマンドを受信するクラス
    ビルド版のUnityから Python を制御するために使用
    
    使用例:
        receiver = OSCCommandReceiver(port=7001)
        receiver.start()
        
        # Unity側で Esc キー押下 → "/unity/command" "quit" が送信される
        # → on_quit_command() が呼ばれる
    """
    
    def __init__(self, port=7001):
        self.port = port
        self.should_quit = False
        self.should_restart = False
        self.should_toggle_debug = False
        self.should_save = False
        self.should_force_send = False
        
        # ディスパッチャー設定
        self.dispatcher = dispatcher.Dispatcher()
        self.dispatcher.map("/unity/command", self.handle_command)
        
        # サーバー
        self.server = osc_server.ThreadingOSCUDPServer(
            ("127.0.0.1", self.port), self.dispatcher
        )
        self.server_thread = None
        
        print(f"[OSCCommandReceiver] Initialized on port {self.port}")
    
    def handle_command(self, address, *args):
        """
        Unityから送信されたコマンドを処理
        """
        if len(args) == 0:
            return
        
        command = args[0]
        print(f"[OSCCommandReceiver] Received command: {command}")
        
        if command == "quit":
            self.should_quit = True
            print("[OSCCommandReceiver] Quit flag set - Application will exit")
        elif command == "restart":
            self.should_restart = True
            print("[OSCCommandReceiver] Restart flag set")
        elif command == "debug":
            self.should_toggle_debug = True
            print("[OSCCommandReceiver] Debug toggle flag set")
        elif command == "save":
            self.should_save = True
            print("[OSCCommandReceiver] Save settings flag set")
        elif command == "force_send":
            self.should_force_send = True
            print("[OSCCommandReceiver] Force send flag set")
    
    def start(self):
        """
        受信スレッドを開始
        """
        self.server_thread = threading.Thread(target=self.server.serve_forever)
        self.server_thread.daemon = True
        self.server_thread.start()
        print("[OSCCommandReceiver] Server thread started")
    
    def stop(self):
        """
        受信を停止
        """
        if self.server:
            self.server.shutdown()
        print("[OSCCommandReceiver] Server stopped")
    
    def check_quit(self):
        """
        終了フラグをチェック
        Returns:
            bool: True なら終了すべき
        """
        if self.should_quit:
            self.should_quit = False  # リセット
            return True
        return False
    
    def check_restart(self):
        """
        リスタートフラグをチェック
        Returns:
            bool: True ならリスタートすべき
        """
        if self.should_restart:
            self.should_restart = False  # リセット
            return True
        return False
    
    def check_debug_toggle(self):
        """
        デバッグ切替フラグをチェック
        Returns:
            bool: True ならデバッグモードを切り替えるべき
        """
        if self.should_toggle_debug:
            self.should_toggle_debug = False  # リセット
            return True
        return False
    
    def check_save(self):
        """
        設定保存フラグをチェック
        Returns:
            bool: True なら設定を保存すべき
        """
        if self.should_save:
            self.should_save = False  # リセット
            return True
        return False
    
    def check_force_send(self):
        """
        強制送信フラグをチェック
        Returns:
            bool: True なら全パラメータを再送すべき
        """
        if self.should_force_send:
            self.should_force_send = False  # リセット
            return True
        return False
