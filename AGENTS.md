# ルール
- 日本語で絵文字を含めて返答します。
- セッション開始時にこのファイルを読みます。コード構造を変えたら更新します。
- 利用者向けの導入・使い方は `README.md`、編集者(AIエージェント)向けの運用知識・契約はこのファイルに書きます。

# アプリ概要
グローバルホットキーで呼び出す検索モーダル型ランチャー(C# / WinForms / net6.0-windows / 依存パッケージなし)。
登録したフォルダを VS Code / Cursor / Antigravity でワンクリックで開く。WSL内パス対応。

- **Ctrl+Alt+Z**: 画面中央に検索モーダルを表示/非表示トグル
- 検索入力欄: 入力で登録パスを部分一致フィルタ(名前・パス・WSLディストリ名、大文字小文字無視)
- `↑` / `↓`: 候補選択を移動(端で折り返し)
- `Enter`: 選択中の候補を開く
- `Tab`: 「設定」「新規追加」ボタンへフォーカス移動
- `Esc` / 再度 `Ctrl+Alt+Z`: モーダルを閉じる
- 候補をクリック: 即起動
- 「設定」ボタン: パス管理・エディタ切替・常に手前に表示・終了
- 「新規追加」ボタン: EntryDialog でパスを追加

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
- `.docs` : 設計資料・リファクタ指示/メモ(`.docs/refactor/` に指示書・調査メモ・提案、`.docs/2026-06-27/` に今回の設計書)
- `source` : ソースコード
  - `Program.cs` : エントリポイント(Mutex で多重起動防止 → `MainForm` 起動)
  - `MainForm.cs` : ホットキー受け取り専用の不可視ウィンドウ(Opacity=0, Size=1x1)。`Ctrl+Alt+Z` で `LauncherDialog` を表示/非表示トグルする
  - `LauncherDialog.cs` : 検索モーダル本体。検索入力欄・「設定」「新規追加」ボタン・候補 `ListBox`(OwnerDraw / MultiColumn)で構成。幅900px、1列445pxの2列表示。名前順に上から下、左列から右列へ並び、最大30行×2列(画面高さで制限)。超過時は横スクロール。`ResetAndShow()` でリセット表示、`RealClose()` でアプリ終了時に Dispose
  - `EntrySearch.cs` : 純粋関数 `Filter(entries, query)` — 名前・パス・WslDistro の部分一致フィルタ、名前昇順を保証
  - `FanPopup.cs` : 旧ホバーパネル(現在は未使用)。`Draw.RoundedRect` ヘルパーはここにある
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
- `MainForm` は Opacity=0/Size=1x1 の不可視ウィンドウ。`MinimumSize`/`MaximumSize` を明示しないと OSの最小ウィンドウサイズ(約136x39)にクランプされるため明示が必須。
- `LauncherDialog.RealClose()` は MainForm の FormClosing から呼ぶ。直接 `Close()` するとフォームが Dispose されてホットキーを押すたびに再生成が必要になる。`FormClosing` をオーバーライドして `_realClose=false` の場合は `Hide()` するのが意図的な設計。
- `LauncherDialog` の高さは表示時(`ResetAndShow`)に `_config.Entries.Count` を基に `ResizeToFit()` で決める。フィルタ変更ごとにはリサイズしない(ちらつき防止)。
- WSL 既定ディストリ名の取得(`Launcher.GetDefaultWslDistro`)は `wsl` を同期実行する。タイムアウト3秒、結果はプロセス内にキャッシュ。UI スレッドを最大3秒ブロックし得る点に注意(非同期化は `.docs/refactor/proposals.md` P2)。
- `WindowX`/`WindowY` は settings.json に残るが、モーダル移行後は使用しない(後方互換維持のため削除しない)。
