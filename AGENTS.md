# ルール
- 日本語で絵文字を含めて返答します。
- セッション開始時にこのファイルを読みます。コード構造を変えたら更新します。
- 利用者向けの導入・使い方は `README.md`、編集者(AIエージェント)向けの運用知識・契約はこのファイルに書きます。

# アプリ概要
デスクトップ常駐の小型ランチャーウィジェット(C# / WinForms / net6.0-windows / 依存パッケージなし)。
登録したフォルダを VS Code / Cursor / Antigravity でワンクリックで開く。WSL内パス対応。

- マウスオーバー: アイコンの下にパス一覧パネルが表示(パネルがスライドイン+行が時差付きフェードイン) → クリックでエディタ起動(左クリックでも出せる)
- 行を右クリック: そのパスの編集ダイアログ
- アイコンを右クリック: 設定メニュー(パス追加・管理 / エディタ切替 / 常に手前に表示 / 終了)
- 左ドラッグ: ウィジェット移動(位置は保存)

# ビルド・テスト・再起動

```
dotnet run                  # ソースから起動(下記の注意あり)
dotnet build                # ビルドのみ
dotnet run --project tests  # テスト(終了コード=失敗数)
.\restart.ps1               # publish 版を再ビルドして常駐を入れ替え
```

- 普段は `publish\OpenFolder\OpenFolder.exe` が常駐している。**Mutex(`Local\open-folder-widget`)の多重起動防止により、常駐中に `dotnet run` しても2個目は静かに即終了する**。コード変更を実機で確認するときは `.\restart.ps1`(常駐終了 → `dotnet publish` → 起動)を使う。
- csproj はルートに置き、`source/` 以下の .cs を暗黙の glob でコンパイルする。`tests/**` は除外済み。
- オフライン環境のため test SDK(xunit / MSTest)が NuGet 復元できず、`tests` は依存パッケージゼロの自前コンソールランナーにしている(`dotnet test` は使えない)。

# 守るべき挙動・契約(壊さないこと)
- **settings.json スキーマ**(保存先 `%APPDATA%\open-folder\settings.json`、PascalCase): `Entries[]{ Name, Path, IsWsl, WslDistro, ChipTheme }, Editor, AlwaysOnTop, WindowX, WindowY`。JSON プロパティ名の変更、旧 `ChipTheme` エイリアス(`DARK1` 等)の受理停止は後方互換を壊す。
- **起動コマンドの形**(`Launcher.Open`): WSL=`cmd /c <editor> --remote wsl+<distro> "<path>"` / Win=`cmd /c <editor> "<path>"`。`UseShellExecute=false`・`CreateNoWindow=true`。
- **Mutex 名** `Local\open-folder-widget`。2個目のインスタンスは静かに終了する。
- パス一覧は常に名前の昇順(`PathEntry.ByName`)。`AppConfig.Load`/`Save` でソートされるため手動並べ替えはない。
- WinForms の地雷回避策(後述の「実装上の注意」)は意図的なもの。安易に外さない。

# ファイル
- `README.md` : 利用者向けの導入・使い方
- `.docs` : 設計資料・リファクタ指示/メモ(`.docs/refactor/` に指示書・調査メモ・提案)
- `source` : ソースコード
  - `Program.cs` : エントリポイント(Mutex で多重起動防止 → `MainForm` 起動)
  - `MainForm.cs` : 常駐ウィジェット本体(ホバー検知・ドラッグ移動・設定メニュー・アイコン描画)
  - `FanPopup.cs` : ホバーで表示するパス一覧パネル。SF調のダーク1枚パネル(白枠(ウィジェット外周と同じ太さ1.6×scale)+シアンの対角コーナーブラケット)に行を縦に並べる。各行はテーマ色で角丸に塗り(`ChipTheme`)、テキストは左揃え・背景輝度に応じて自動で黒/白。ホバー行は明るく補正。per-pixelアルファの1枚レイヤードウィンドウ(`UpdateLayeredWindow`)にGDI+描画。透明部分は自動クリックスルー、`WS_EX_NOACTIVATE`でフォーカスを奪わない。角丸描画ヘルパー `Draw` もここ
  - `Launcher.cs` : エディタ起動コマンド(WSL: `cmd /c <editor> --remote wsl+<distro> "<path>"` — Windows側エディタCLIへWSLリモートとして渡す / Win: `cmd /c <editor> "<path>"`)。`\\wsl.localhost\...`・`\\wsl$\...` → WSL内パス+ディストリ名へ変換。`WslDistro` が未設定のWSLエントリは `wsl` から既定ディストリ名を取得して補う
  - `ChipThemes.cs` : テーマ名 → 色の解決(`NormalizeChipTheme` / `GetChipColor` / `GetFontColorForBackground`)。旧エイリアス受理を含む。描画層専用で設定モデルからは分離
  - `AppConfig.cs` : 設定の読み書きと `PathEntry` モデル(保存先: `%APPDATA%\open-folder\settings.json`)。読めない設定は `settings.json.bak` へ退避してから初期化する
  - `EntryDialog.cs` : パス追加・編集ダイアログ(`/`始まりはWSL自動判定。名前は `{環境} : {親フォルダ} - {フォルダ名}` 形式で自動生成 — 例 `WSL : github - hoge`, `C : github - huga`)
  - `ManageDialog.cs` : パス一覧の管理(追加・編集・削除)ダイアログ
- `tests` : 純粋関数の characterization テスト(依存パッケージゼロの自前ランナー)。`dotnet run --project tests` で実行、終了コード=失敗数
- `open-folder.csproj` : net6.0-windows / WinForms(SDKは6.0.300のみインストール済み)。`tests/**` は暗黙globから除外し、`OpenFolder.Tests` への `InternalsVisibleTo` を付与
- `restart.ps1` : 常駐 exe の終了 → publish → 再起動を一括で行うスクリプト

# 実装上の注意
- 多重起動はMutex(`Local\open-folder-widget`)で防止。2個目は起動直後に静かに終了する。
- ウィジェットは `MinimumSize`/`MaximumSize` の明示が必須。ボーダーレスでもOSの最小ウィンドウサイズ(約136x39)にクランプされ、細い帯になって見失う事故が過去にあった。
- アイコンは `MainForm.OnPaint` で `GraphicsPath` によるベクター描画(SVG相当)。56単位の論理座標系で描き、DPIに応じてスケールする。
- 初期表示位置はプライマリ画面の作業領域(タスクバー除く)右下。
- WSL 既定ディストリ名の取得(`Launcher.GetDefaultWslDistro`)は `wsl` を同期実行する。タイムアウト3秒、結果はプロセス内にキャッシュ。UI スレッドを最大3秒ブロックし得る点に注意(非同期化は `.docs/refactor/proposals.md` P2)。
