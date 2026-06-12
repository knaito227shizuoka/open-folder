# refactor 調査メモ

調査・実装中に気づいたことを追記していくメモ。指示書本体は `refactor-instructions.md`。

## 2026-06-12 初回調査(Claude / 分析担当)

### 読んだもの
- AGENTS.md / CLAUDE.md / README.md / open-folder.csproj / restart.ps1
- source/ 全7ファイル(Program, MainForm, FanPopup, Launcher, AppConfig, EntryDialog, ManageDialog)
- git log(4コミット、初コミットは 4580c18)

### 事実確認できたこと
- 依存パッケージなし(csproj は素の net6.0-windows + WinForms のみ)。SDK は 6.0.300 のみ。
- テストプロジェクトなし。検証は `dotnet build` と手動操作のみ。
- `.docs` ディレクトリは AGENTS.md に「資料」と記載があるが実在しなかった(このフォルダが初の中身)。
- 設定は `%APPDATA%\open-folder\settings.json`。スキーマは AppConfig/PathEntry の public プロパティそのまま(camelCase化なし、PascalCase)。
- 普段は `publish\OpenFolder\OpenFolder.exe` が常駐しており、Mutex(`Local\open-folder-widget`)のせいで `dotnet run` は静かに即終了する。再起動は `.\restart.ps1`。

### 気になったこと(指示書に反映済み)
- `Launcher.GetDefaultWslDistro` は UI スレッドで `ReadToEnd()` ブロック。WSL コールドスタートだと数秒固まる可能性。`WaitForExit(3000)` の戻り値も未チェックで、タイムアウト時に `ExitCode` 参照で例外になる経路がある。
- ルート csproj は暗黙 glob で `**/*.cs` を拾うため、`tests/` を作ると本体に巻き込まれる。`<Compile Remove>` が必須。
- `PathEntry.NormalizeChipTheme` の `DARK1`/`LIGHT2` 等のエイリアスは旧保存形式の互換とみられる(現コードでは生成されない値)。削除可否はユーザー確認が必要。
- README の「パスの種類」見出しの下がスタートアップ登録手順になっている(ba1a7f5 で置換)。意図か事故か不明 → 質問化。
- `AppConfig.Load` は全例外を握りつぶして新規 config を返すため、一時的な IO エラー → 次の `Save()` で登録パスが全消滅、というデータ消失経路がある。
- `cmd /c {editor} ...` の `editor` は settings.json 由来の文字列が未クォートで埋め込まれる。ローカル設定ファイルなので深刻度は低いが、境界として認識しておく。
- パス内の `%` は cmd のクォート内でも環境変数展開される。実害が出るフォルダ名は稀なので提案止まり。

### 未解決 / 様子見
- FanPopup の `Push()` は 15ms ごとに HBITMAP を作り直す。パネルが小さいので実測上問題が出てからでよい。
- `Yu Gothic UI` ハードコード(FanPopup / 各ダイアログ)。日本語環境前提なら問題なし。

---

(以降、実装担当が気づきを追記すること)
