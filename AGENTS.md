# ルール
- 日本語で絵文字を含めて返答します。

# アプリ概要
デスクトップ常駐の小型ランチャーウィジェット(C# / WinForms / 依存パッケージなし)。
登録したフォルダを VS Code / Cursor / Antigravity でワンクリックで開く。WSL内パス対応。

- マウスオーバー: アイコンの下にパス一覧パネルが表示(パネルがスライドイン+行が時差付きフェードイン) → クリックでエディタ起動(左クリックでも出せる)
- 右クリック: 設定メニュー(パス追加・管理 / エディタ切替 / 常に手前に表示 / 終了)
- 左ドラッグ: ウィジェット移動(位置は保存)

# ビルド・起動

```
dotnet run
```

csproj はルートに置き、`source/` 以下の .cs は暗黙の glob で自動的にコンパイル対象になる。

# ファイル
- `.docs` : 資料
- `source` : ソースコード
  - `Program.cs` : エントリポイント
  - `MainForm.cs` : 常駐ウィジェット本体(ホバー検知・ドラッグ移動・設定メニュー・アイコン描画)
  - `FanPopup.cs` : ホバーで表示するパス一覧パネル。SF調のダーク1枚パネル(シアンの細枠+対角コーナーブラケット)に行を縦に並べ、テキストは左揃え・各行左端にテーマ色のアクセントバー(暗い色はパネルに埋もれないよう自動で明るく補正)。per-pixelアルファの1枚レイヤードウィンドウ(`UpdateLayeredWindow`)にGDI+描画。透明部分は自動クリックスルー、`WS_EX_NOACTIVATE`でフォーカスを奪わない。角丸描画ヘルパー `Draw` もここ
  - `Launcher.cs` : エディタ起動コマンド(WSL: `wsl -e bash -lc "code '<path>'"` / Win: `cmd /c code "<path>"`)と `\\wsl.localhost\...` → WSLパス変換
  - `AppConfig.cs` : 設定の読み書き(保存先: `%APPDATA%\open-folder\settings.json`)
  - `EntryDialog.cs` : パス追加・編集ダイアログ(`/`始まりはWSL自動判定。名前は `{環境} : {親フォルダ} - {フォルダ名}` 形式で自動生成 — 例 `WSL : github - hoge`, `C : github - huga`)
  - `ManageDialog.cs` : パス一覧の管理(追加・編集・削除)ダイアログ
- パス一覧は常に名前の昇順(`PathEntry.ByName`)。`AppConfig.Load`/`Save` でソートされるため手動並べ替えはない
- `open-folder.csproj` : net6.0-windows / WinForms(SDKは6.0.300のみインストール済み)

# 実装上の注意
- 多重起動はMutex(`Local\open-folder-widget`)で防止。2個目は起動直後に静かに終了する。
- ウィジェットは `MinimumSize`/`MaximumSize` の明示が必須。ボーダーレスでもOSの最小ウィンドウサイズ(約136x39)にクランプされ、細い帯になって見失う事故が過去にあった。
- アイコンは `MainForm.OnPaint` で `GraphicsPath` によるベクター描画(SVG相当)。56単位の論理座標系で描き、DPIに応じてスケールする。
- 初期表示位置はプライマリ画面の作業領域(タスクバー除く)右下。
