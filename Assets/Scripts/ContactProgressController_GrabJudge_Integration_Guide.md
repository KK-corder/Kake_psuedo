## ContactProgressController.cs への GrabJudge 連携追加

### 実装内容

1. **参照の追加**
```csharp
[Header("References")]
public BoneJudgeNew bonejudgeNew;
public GrabJudge grabJudge; // 握り判定用 <- 追加
```

2. **発動条件設定の追加**
```csharp
[Header("Activation Conditions")]
public bool requireGrabAndContact = true; // 握り+接触の両方を必要とする
public bool requireContactOnly = false; // 接触のみで発動（従来動作）
```

3. **発動条件チェック機能**
```csharp
// 握り状態をチェック
bool grabActive = false;
if (grabJudge != null)
{
    grabActive = grabJudge.IsAnyHandGrabbing();
}

// 発動条件をチェック
bool shouldActivateProgress = CheckActivationConditions(contactActive, grabActive);
```

4. **CheckActivationConditions メソッド**
```csharp
private bool CheckActivationConditions(bool isContacting, bool isGrabbing)
{
    if (requireGrabAndContact)
    {
        // 握り+接触の両方が必要
        return isContacting && isGrabbing;
    }
    else if (requireContactOnly)
    {
        // 接触のみで発動（従来動作）
        return isContacting;
    }
    else
    {
        // デフォルト: 握り+接触の両方が必要
        return isContacting && isGrabbing;
    }
}
```

### 設定方法

#### インスペクターでの設定:
1. **Grab Judge**: 作成したGrabJudgeコンポーネントを参照
2. **Require Grab And Contact**: ✓ チェック（握り+接触の両方が必要）
3. **Require Contact Only**: ✗ チェック外す

#### 動作モード:
- **標準モード** (Require Grab And Contact = true): 握って、かつ接触している時にProgress処理が実行
- **接触のみモード** (Require Contact Only = true): 従来通り接触のみでProgress処理が実行

### 発動条件の仕組み

#### Before（従来）:
```
接触判定 = true → Progress処理実行
```

#### After（新仕様）:
```
接触判定 = true AND 握り判定 = true → Progress処理実行
```

### デバッグ情報

有効にした場合、以下のようなログが出力されます:
```
ContactProgressController: RequireGrabAndContact mode - Contact: true, Grab: true, Activate: true
Activation Status: Mode: GrabAndContact, Contact: true, Grab: true, Progress Active: true
```

これで、握って接触している時のみcurrentProgressが変化し、シェーダーの色変化が発生するようになります。