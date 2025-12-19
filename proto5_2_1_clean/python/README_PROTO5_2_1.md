# プロト5.2.1 - 手の反転処理修正版

## 保存日時
2025年12月17日 19:36

## バックアップ場所
- Python: `/Users/asanolab/Desktop/VL2025.12.5/python/proto5_2_1/`
- Unity: `/Users/asanolab/Desktop/VL2025.12.5/unity_backup/proto5_2_1/`

## 主要な変更内容（5.2 → 5.2.1）

### 1. 手の反転処理の修正

**SkeletonController.cs**（485-496行目）

手の画像表示を正しく調整：

```csharp
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
```

**反転設定**:
- **Hand_L**（画面上左、ユーザーの右手）: flipY のみ（上下反転）
- **Hand_R**（画面上右、ユーザーの左手）: flipY + flipX（上下反転 + 左右反転）

### 2. handAdditionalRotation削除（継承）

Proto 5.2から継承：
- Unity Inspector の手動角度調整機能を削除
- シンプルなコード構造

## プロト5.2.1の全機能

### 新機能（5.0 → 5.2.1）

1. **浮遊する塵（Dust Motes）**
   - ワールド座標固定のパーティクルシステム
   - スポットライト内でのみ表示
   - Inspector で速度調整可能
   - グラデーションマスクとの連携

2. **スポットライト改善**
   - Awake()メソッドによるAnimator自動起動防止
   - グラデーションマスク（境界での自然なフェードアウト）

3. **手の表示修正**
   - 上下反転処理
   - 左手のみ左右反転
   - Python側の姿勢と一致

### 継承機能（5.1から）

- **MusicManager.cs**: フェーズに応じた音楽自動切り替え
- **GhostLayoutManager.cs**: セクションベースのエネルギー連動
- **動的召喚演出**: 亡霊の順次フェードイン

## Unity側での確認事項

1. **DustMotes オブジェクト**
   - Position: (0, 0, -1)
   - DustMotesController.cs がアタッチされている
   - Max Particles: 300

2. **Spotlight**
   - AnimatorのPlay on AwakeがOFF
   - グラデーションマスクが適用されている

3. **Hand（手）の表示**
   - 両手が正しい向きで表示される
   - ユーザーの動きと一致

## 復元方法

```bash
# Python
cp -r python/proto5_2_1/* python/proto4_1/

# Unity
cp unity_backup/proto5_2_1/*.cs unity_assets/
```

## 既知の問題・制限事項

特になし

## 次の開発ステップ

1. 足の画像反転の確認
2. 腕・脚の画像反転の確認
3. パフォーマンス最適化
