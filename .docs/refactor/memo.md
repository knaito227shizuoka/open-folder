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

## 2026-06-12 実装(Claude / 実装担当・Phase 1)

### 重大な環境制約: オフライン
- このマシンは NuGet オフラインフィード(`Microsoft Visual Studio Offline Packages`)のみ。ネットワーク復元不可。
- xunit / xunit.runner.visualstudio はフィードに存在しない。
- MSTest(`mstest.testadapter` 2.2.7 等)は存在するが、`Microsoft.NET.Test.Sdk 16.11.0` が `NuGet.Frameworks >= 5.0.0` を要求し、フィードには 4.6.4 しか無いため `dotnet test` 系は復元不可。
- **結論**: 指示書 D1 の「xunit でテスト」は環境的に不可能。依存パッケージゼロの**自前コンソールランナー**(`tests/OpenFolder.Tests.csproj` を `OutputType=Exe`)に切替。
  - 実行コマンドは `dotnet test` ではなく **`dotnet run --project tests`**(終了コード = 失敗数)。
  - characterization test の目的(退行検出)は同等に達成。ネットワークが復活したら xunit へ移行してよい(提案: proposals.md には未記載、軽微なので memo に留める)。

### Phase 1 でやったこと
- `open-folder.csproj` に `<Compile Remove="tests/**/*.cs" />`(暗黙glob除外)と `<InternalsVisibleTo Include="OpenFolder.Tests" />` を追加。
- `Launcher.QuoteCmdArgument` を private → internal(テスト用。挙動不変)。
- `tests/` に純粋関数4つ(GenerateName / TryConvertUncWslPath / NormalizeChipTheme / QuoteCmdArgument)の characterization test を追加。**25 ケース全 PASS**。
- 本体 `dotnet build` は警告0のまま、`dotnet run`/`restart.ps1` への影響なし(tests はサブディレクトリの別プロジェクトなので root の publish/build は拾わない)。

## 2026-06-12 実装(Phase 2-3)

### Phase 2(ドキュメント整合)
- AGENTS.md の Launcher 行を現実装(`cmd /c <editor> --remote wsl+<distro>`)に更新。
- ついでに FanPopup 行の「各行左端にテーマ色のアクセントバー」が 83253fb で削除済みの旧仕様だったので訂正(行全体をテーマ色で塗る現仕様に)。証拠: git 83253fb "Remove popup row accent bars"。
- tests 追加に伴いファイル一覧とテストコマンドを追記。README は Q1 回答待ちのため未変更。

### Phase 3(防御的補強)
- D2①②: `GetDefaultWslDistro` を「WaitForExit を先に呼んでタイムアウトを実効化 → Kill → 明示メッセージ例外」に変更し、結果を static キャッシュ。ReadToEnd の完全 async 化は提案止まり(proposals.md)。
- D3: `AppConfig.Load` の catch で `settings.json` → `settings.json.bak` 退避を追加(挙動は従来どおり初期化を続行、データ救済の余地だけ追加)。
- D8①: `FanPopup.Dispose(bool)` で `_anim`/`_frame` を破棄。
- 実機検証: 壊れ JSON 投入 → restart → `.bak` 生成を確認。実設定(17エントリ)復元 → 正常起動・新規 .bak なしを確認。build 警告0・テスト 25 PASS。
- **GUI クリック系スモーク(ホバー表示・行クリック起動・ドラッグ移動)は自動化できず未検証**。人手確認が必要。

## 2026-06-12 実装(Phase 4-5)

### Phase 4(責務分離 D4)
- `NormalizeChipTheme`/`GetChipColor`/`GetFontColorForBackground` を `PathEntry` から新規 `source/ChipThemes.cs` へ移動。ロジック・出力は不変(テスト 25 PASS で確認)。
- 呼び出し元(FanPopup×2, EntryDialog×2)とテスト参照を `ChipThemes.*` に差し替え。`PathEntry.ChipTheme`(JSON プロパティ)は不変。
- `PathEntry.NormalizeChipTheme` という公開 static は無くなったので、外部から参照していたコードがあれば破壊的(このリポジトリ内では全て追従済み)。

### Phase 5(提案のみ)
- `proposals.md` に P1〜P6 を記載。いずれも未実装。Q3(WslDistro 永続化)・Q4(Editor 契約)・Q5(リネーム)の回答が前提の項目を含む。
- 触らないと決めた軽微項目(DisplayName パススルー / DPI 計算重複 / Push の HBITMAP)は proposals 末尾に記録。

### 全フェーズ完了時点の状態
- build 警告0、テスト 25 PASS、アプリ常駐 OK。
- 残課題: GUI クリック系スモーク(②ホバー表示 ③Windowsパス起動 ④WSLパス起動 ⑤管理ダイアログ ⑥ドラッグ移動)は人手確認待ち。README は Q1 回答待ちで未変更。

---

(以降、実装担当が気づきを追記すること)
