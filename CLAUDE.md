# Kake_psuedo — 開発マニュアル

VR 疑似触覚（pseudo-haptics）実験の Unity プロジェクト。`Kake_VR_pseudohaptics` と同系統で、
WISS デモ（`WISSdemo.cs`）を含む派生。Claude Code を使った開発フローと仕様を記録する。

---

## プロジェクト概要

（要記入 / 要確認: 本プロジェクト固有の実験目的・条件。以下は Assets 構成から読み取れる範囲の暫定記述）

- Oculus/Meta Quest のハンドトラッキングを用いた VR 疑似触覚実験。
- `HandTransitionController` による手の見た目遷移、`BoneJudgeNew*` による接触/骨判定を持つ。
- `WISSdemo.cs` を用いた WISS 向けデモ構成が含まれる。

## 環境

- Unity `2022.3.28f1`

## ディレクトリ構成

```
Assets/
  Scripts/        … 実験ロジック（下記「主要スクリプト」参照）
  Scenes/         … BigGuy / SimpleScene / SampleScene / Simple FPS Example
  Prefabs/ Materials/ Resources/
  Human/ BigGuy/                     … アバター/モデル
  Oculus/ MetaXR/ XR/               … Meta XR / OpenXR 関連
  Plugins/                          … 外部プラグイン（FinalIK 等・使用時）
  humanoidcontrol4_free/ Unity-Movement-main/ InteractMotions/
  HandPoseTransferForOculusQuest-master/ UmebocDC_Hand.211010/  … ハンド/モーション系アセット
  Simple FPS Controller/ PlayFromHere/ PretoriusLab/ bibs_v100/ … 補助アセット
  StreamingAssets/
Packages/         … UPM 依存（Meta XR SDK 等）
ProjectSettings/  … Unity プロジェクト設定
Docs/             … サブシステム別マニュアル・設計メモ
```

## 主要スクリプト（Assets/Scripts）

- 判定系: `BoneJudgeNew.cs` / `BoneJudgeNew_2afc.cs` / `BoneJudgeNew_reset.cs` / `GrabJudge.cs`
- 進行/タスク: `ContactProgressController.cs` / `ProgressSet.cs` / `TaskManage.cs` / `EnterReset.cs`
- オブジェクト移動: `CubeMovementController.cs` / `CubeInterval.cs` / `DumbbellMovement.cs`
- 手の表現: `HandTransitionController.cs` / `VisualHand.cs`
- デモ/入出力/補助: `WISSdemo.cs` / `CSVloader.cs` / `PointMarker.cs` / `GrabDebugAnalyzer.cs`

（各スクリプトの責務詳細は `Docs/` に順次分割して記載する）

---

## 開発の約束事

- 原則 `1スクリプト1責務`。
- 実行時出力（`*Results*.csv` / `TaskMetrics*.csv` 等）はコミットしない（`.gitignore` 済み）。
  ※ `SaveData.csv` は現状追跡されている。扱いは別途整理（ワークスペース TODO 参照）。
- ブランチ戦略はワークスペース方針に従う（`main` 安定 / 開発ブランチ / 完了作業 `archive/*`）。
