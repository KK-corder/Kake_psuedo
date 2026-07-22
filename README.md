# Kake_psuedo

VR 疑似触覚（pseudo-haptics）実験用 Unity プロジェクト（`Kake_VR_pseudohaptics` と同系統の派生）。
WISS デモ（`WISSdemo.cs`）を含む。

> ※フォルダ名 `Kake_psuedo` は "pseudo" のタイプミス。リネームは worktree/参照への影響確認後に検討（ワークスペース `README.md` の TODO 参照）。
> ドキュメント構成は `Kake_VR_pseudohaptics` の規約に合わせている（README / CLAUDE.md / _TODO.md / Docs/）。

---

## 環境

- **Unity: `2022.3.28f1`**（厳密一致）
- 対象: Oculus/Meta Quest ハンドトラッキング

## ブランチ

- `main` … 安定
- `develop_base` … 開発基盤
- `wiss_demo` … WISS デモ（現行の作業ブランチ）

## clone 後のセットアップ（他マシンで動かす前に）

1. **Unity バージョンを厳密一致**させる（`2022.3.28f1`）。
2. **Meta XR SDK** は UPM（`Packages/manifest.json` / `packages-lock.json`）から復元される。失敗時は Package Manager から入れ直す。
3. **FinalIK（RootMotion）** を使う場合は有償アセットのため手動インポート（`Assets/Plugins/RootMotion/`）。※本プロジェクトでの要否は要確認。
4. CSV 出力先の絶対パスがシーンに保存されている場合があるため、実行前に Inspector で確認する（既知の注意点）。

## 実行方法

（要記入: 起動シーン・操作手順・デモフロー）

- 主なシーン: `SampleScene.unity` / `SimpleScene.unity` / `BigGuy.unity` / `Simple FPS Example.unity`

---

詳細は `CLAUDE.md`（開発マニュアル）と `Docs/` を参照。
