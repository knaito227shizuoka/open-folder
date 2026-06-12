# open-folder リファクタリング指示書

実装担当モデルへ: この文書は分析担当が全ソースを読んだうえで作成した。証拠(ファイル:行)に基づかない大きな削除・全面書き換えはしないこと。迷ったら止まって質問すること。

## Objective

既存仕様(設定ファイル互換・操作感・起動コマンド)を一切壊さずに、(1) 検証手段がない状態を解消し、(2) UI フリーズやデータ消失につながる既知のリスク箇所を補強し、(3) 責務の混在と命名のズレを小さく整理して、今後の変更を安全にする。見た目の綺麗さは目的ではない。

## Project Understanding

- **何のアプリか**: デスクトップ常駐の小型ランチャーウィジェット(C# / WinForms / net6.0-windows / 依存パッケージなし)。登録フォルダを VS Code / Cursor / Antigravity でワンクリックで開く。WSL 内パス対応。
- **ユーザー体験**: アイコンにホバー(or 左クリック)→ パス一覧パネルが出る → 行クリックでエディタ起動 / 行右クリックで編集。アイコン右クリックで設定メニュー。左ドラッグで移動(位置保存)。
- **エントリーポイント**: `source/Program.cs` — Mutex(`Local\open-folder-widget`)で多重起動防止後、`MainForm` を `Application.Run`。
- **モジュールと責務**:
  - `source/MainForm.cs` — 常駐ウィジェット本体。ホバー検知(100ms ポーリングタイマー)、ドラッグ移動、設定メニュー、アイコンのベクター描画(56単位論理座標、DPI スケール)。
  - `source/FanPopup.cs` — パス一覧パネル。per-pixel アルファのレイヤードウィンドウ(`UpdateLayeredWindow` P/Invoke)に GDI+ で全描画。`WS_EX_NOACTIVATE` でフォーカスを奪わない。角丸ヘルパー `Draw` も同居。※名前は旧・扇型展開時代の名残で、現在は縦パネル。
  - `source/Launcher.cs` — エディタ起動。WSL は `cmd /c {editor} --remote wsl+{distro} "{path}"`、Windows は `cmd /c {editor} "{path}"`。UNC(`\\wsl.localhost\...`/`\\wsl$\...`)→ WSL パス+ディストリ名の変換、既定ディストリ名の取得(`wsl -e sh -lc` 実行)。
  - `source/AppConfig.cs` — `PathEntry`(モデル+テーマ色変換+名前自動生成)と `AppConfig`(JSON 読み書き。保存先 `%APPDATA%\open-folder\settings.json`)。
  - `source/EntryDialog.cs` / `source/ManageDialog.cs` — パスの追加・編集 / 一覧管理ダイアログ。
- **データフロー**: settings.json → `AppConfig.Load`(起動時、名前昇順ソート)→ `MainForm` がポップアップ項目に変換 → クリックで `Launcher.Open` → 外部プロセス起動。編集は ダイアログが `PathEntry` を直接ミューテート → `AppConfig.Save`(ソートして全書き込み)。
- **外部依存**: NuGet なし。実行時依存は `cmd`、`wsl`、各エディタの CLI シム(`code`/`cursor`/`antigravity`)、user32/gdi32(P/Invoke)。
- **検証コマンド**: `dotnet build --nologo -v q`(警告0が現状)。動作確認は `.\restart.ps1`(常駐 exe 終了 → publish → 起動)。**`dotnet run` は publish 版が常駐中だと Mutex で静かに即終了する**ので注意。
- **運用**: `publish\OpenFolder\OpenFolder.exe` のショートカットを `shell:startup` に置いて常駐させる(README)。

## Behaviors To Preserve(絶対に壊さない既存挙動)

1. **settings.json の後方互換**。スキーマ(PascalCase): `Entries[]{Name, Path, IsWsl, WslDistro, ChipTheme}, Editor, AlwaysOnTop, WindowX, WindowY`。既存ファイルが読めなくなる変更・JSON プロパティ名の変更は禁止。`ChipTheme` の旧エイリアス(`DARK1` 等 → `AppConfig.cs:17-37`)の受理も維持。
2. **起動コマンドの形**(`Launcher.cs:21-47`)。WSL: `cmd /c {editor} --remote wsl+"{distro}" "{path}"` / Windows: `cmd /c {editor} "{path}"`。`UseShellExecute=false` + `CreateNoWindow=true`。
3. **Mutex 名** `Local\open-folder-widget`(`Program.cs:9`)。2個目のインスタンスは静かに終了する挙動。
4. **UI 挙動**: ホバーでパネル表示・カーソルが離れるとフェードアウト、行左クリック=起動・行右クリック=編集、アイコン右クリック=メニュー、5px 閾値のドラッグ移動と位置保存、パス一覧は常に名前昇順。
5. **WinForms の地雷回避策**: `MainForm` の `MinimumSize`/`MaximumSize` 明示(`MainForm.cs:33-35`、OS 最小サイズへのクランプ事故防止)、`FanPopup` の `ShowWithoutActivation`/`WS_EX_LAYERED|TOOLWINDOW|NOACTIVATE`(`FanPopup.cs:87-99`)。
6. **名前の自動生成規則**(`AppConfig.cs:61-72`): `WSL : github - hoge` / `C : github - huga` / ドライブ直下は親省略。
7. **UNC → WSL パス変換**(`Launcher.cs:56-71`): `\\wsl.localhost\` と `\\wsl$\` の両プレフィックス、大文字小文字無視。

## Non-Negotiables

- 最初に `git status` を確認する。**既存の未コミット変更と自分の変更を混ぜない**(着手時に未コミット差分があれば報告して指示を待つ)。
- 編集前に Baseline Commands の結果を記録する。
- 変更は小さく、フェーズ単位で戻せるコミットにする。
- 無関係な整形・ついでのリファクタリングをしない。
- 既存挙動を勝手に変えない。正しさが不明なら実装を止めて質問する。
- 各フェーズ完了ごとに Verification Requirements を実施する。
- 証拠なくコードを削除しない(特に `NormalizeChipTheme` のエイリアス群)。
- AGENTS.md のルールに従い、構造を変えたら AGENTS.md を更新する。

## Stop And Ask Conditions(該当したら実装を止めて質問)

- settings.json に**書き込まれる内容**が変わる変更(例: `WslDistro` の自動保存)。
- JSON プロパティ名・公開クラス名のうちシリアライズに関わるものの変更。
- `NormalizeChipTheme` のエイリアス削除など、保存済みデータの受理範囲を狭める変更。
- 起動コマンド文字列の形が変わる変更(クォート方式の変更を含む)。
- テストと実装が矛盾していると思われた場合(このリポジトリにはまだテストがないため、新規テストの期待値が現実装と食い違ったら「実装が正」とせず質問)。
- 削除候補(例: `PathEntry.DisplayName`)が本当に未使用と断定できない場合。

## Baseline Commands

```powershell
git status --short            # 未コミット差分の確認(必須・最初)
dotnet build --nologo -v q    # 期待: ビルド成功・警告0
.\restart.ps1                 # publish 版を再ビルドして再起動(動作確認用)
Get-Process OpenFolder        # 常駐確認
```

手動スモーク(変更後に毎回): ① ウィジェット表示 ② ホバーでパネル表示 ③ Windows パスの行クリックでエディタが開く ④ WSL パスの行クリックで WSL リモートとして開く ⑤ 右クリックメニューから「パスの管理」→ 追加・編集・削除 ⑥ ドラッグ移動 → 再起動で位置復元。

## Debt Map

### D1. テスト皆無(純粋ロジックも未保護)— 最優先で安全網
- **根拠**: リポジトリにテストプロジェクトなし。`PathEntry.GenerateName`(`AppConfig.cs:61-72`)、`Launcher.TryConvertUncWslPath`(`Launcher.cs:56-71`)、`NormalizeChipTheme`(`AppConfig.cs:17-37`)、`QuoteCmdArgument`(`Launcher.cs:92-95`)は UI 非依存の純粋関数。
- **なぜ負債か**: 仕様(名前生成規則・UNC 変換・テーマ互換)がコードにしか存在せず、リファクタの退行を検出できない。
- **影響範囲**: 全リファクタ作業の安全性。
- **変更リスク**: 低。ただし**ルート csproj は暗黙 glob で `**/*.cs` を拾う**ため、`tests/` を作ると本体ビルドに巻き込まれる。`open-folder.csproj` に `<Compile Remove="tests/**" />` を追加するか、それに相当する除外が必須。`dotnet run`(引数なし)が壊れないことも確認する。
- **改善案**: `tests/OpenFolder.Tests.csproj`(net6.0-windows, xunit, SDK 6.0.300 で動くバージョン)を追加し、上記4関数の現挙動を固定する characterization test を書く。期待値は**現実装の出力**(例: `GenerateName("C:\\github\\huga", false)` → `"C : github - huga"`、`GenerateName("/home/u/p", true)` → `"WSL : u - p"`、ドライブ直下 `"C:\\proj"` → `"C : proj"`)。
- **検証**: `dotnet test tests` が成功し、`dotnet build` / `dotnet run` も従来どおり動く。
- **実装可否**: **今実装してよい**。

### D2. `GetDefaultWslDistro` の UI スレッドブロック+タイムアウト時の例外経路
- **根拠**: `Launcher.cs:73-90`。`MainForm.OpenEntry`(`MainForm.cs:288-299`)から UI スレッドで呼ばれ、`ReadToEnd()` が wsl プロセス終了までブロックする(WSL コールドスタートで数秒固まり得る)。さらに `WaitForExit(3000)` の戻り値(bool)を見ておらず、タイムアウト時は未終了プロセスの `ExitCode` 参照で `InvalidOperationException`(メッセージが意図したものと違う例外)になる。毎回 wsl を起動し、結果をキャッシュしない。
- **なぜ負債か**: 体感フリーズと、わかりにくいエラーメッセージという実害につながる。
- **影響範囲**: WSL エントリのうち `WslDistro` が空のもの(UNC 経由でなく `/home/...` を手入力したエントリ)の起動時のみ。
- **変更リスク**: 低〜中。起動コマンド自体は変えない。
- **改善案**: ① `WaitForExit` の戻り値を確認し、タイムアウト時は `process.Kill()` のうえ意図したメッセージの例外を投げる。② 取得結果をプロセス内 static にキャッシュする(初回のみ実行)。③ settings.json への永続化は **Stop And Ask**(保存データが変わるため Q3 を先に確認)。UI の非同期化(async 化)は影響が広いので提案止まり。
- **検証**: D1 で `TryConvertUncWslPath` を固定後、WSL エントリの手動起動確認(スモーク④)。タイムアウト経路は `wsl` が存在しない環境を作れないため、コードレビューで確認。
- **実装可否**: ①②は**今実装してよい**。③と async 化は**提案のみ**。

### D3. `AppConfig.Load` の全例外握りつぶし → `Save` で設定全消失の経路
- **根拠**: `AppConfig.cs:92-108` の `catch { }`(コメント「壊れた設定ファイルは初期化する」)と `Save`(`AppConfig.cs:110-116`)の全上書き。一時的な IO エラー(ファイルロック等)でも空 config が返り、その後の操作(位置保存・メニュー操作だけでも `Save` が走る)で登録パスが全部消える。
- **なぜ負債か**: ユーザーデータ(登録パス)のサイレント消失リスク。意図は「壊れた JSON の初期化」だが、catch の範囲が広すぎて意図以上のケースを飲み込む。
- **影響範囲**: 設定の読み書き全般。
- **変更リスク**: 低(追加的な防御)。
- **改善案**: Load が「ファイルは存在するが読めなかった」場合、上書き前に `settings.json` を `settings.json.bak` へコピーしてから初期化を続行する(挙動は従来どおり初期化、ただし救済可能にする)。catch を `JsonException` / `IOException` に絞るのは挙動変化の検討が要るため提案止まりでもよい。
- **検証**: 壊れた JSON を手で置いて起動 → .bak が作られ、アプリは初期状態で起動することを確認。正常 JSON では従来どおり。
- **実装可否**: .bak 退避は**今実装してよい**。catch 絞り込みは**提案のみ**。

### D4. `PathEntry`(設定モデル)に表示色ロジックが混在
- **根拠**: `AppConfig.cs:39-58` の `GetChipColor` / `GetFontColorForBackground` が設定モデルに同居し、`FanPopup.cs:115-122` と `EntryDialog.cs:73` が参照。
- **なぜ負債か**: 設定の読み書き(永続化層)とテーマパレット(描画層)が同一クラスで、テーマ追加・変更のたびに設定モデルを触ることになる。依存方向(UI → モデル)は正しいが責務が混ざっている。
- **影響範囲**: FanPopup / EntryDialog の参照箇所のみ。シリアライズには無関係(static メソッドなので JSON に出ない)。
- **変更リスク**: 低。機械的な移動。
- **改善案**: `source/ChipThemes.cs`(static class)へ `NormalizeChipTheme` / `GetChipColor` / `GetFontColorForBackground` を移動し、参照を差し替える。`PathEntry.ChipTheme`(JSON プロパティ)はそのまま。
- **検証**: ビルド+D1 のテスト(移動後も同テストが通るよう参照先だけ変える)+スモーク②(パネルの行の色)。
- **実装可否**: **今実装してよい**。

### D5. ドキュメントと実装の不一致
- **根拠**: ① AGENTS.md の `Launcher.cs` 説明が旧方式 `wsl -e bash -lc "code '<path>'"` のまま(実装は ba1a7f5 で `--remote wsl+` に変更済み)。② README の「パスの種類」見出しの下が Windows スタートアップ登録手順になっており見出しと内容が不一致(ba1a7f5 で WSL/Windows パスの説明が削除された)。③ AGENTS.md が `.docs : 資料` と記すが、このフォルダ以前 `.docs` は存在しなかった。
- **なぜ負債か**: AGENTS.md はセッション開始時に必ず読まれる前提のファイルであり、嘘があると後続のエージェント作業全体を誤導する。
- **影響範囲**: ドキュメントのみ。
- **変更リスク**: なし(①③)。②は意図不明のため **Q1 を先に確認**。
- **改善案**: ① AGENTS.md の Launcher 行を現実装(`cmd /c {editor} --remote wsl+<distro>` / 既定ディストリ自動取得 / `WslDistro` 保持)に書き直す。③ `.docs/refactor` が今回でき たので記載は整合済みか確認のみ。② は回答待ち。
- **検証**: 目視レビュー。
- **実装可否**: ①③は**今実装してよい**。②は**質問の回答後**。

### D6. 命名の歴史的ズレ(扇型・チップ時代の名残)
- **根拠**: `FanPopup`(現在は扇型でなく縦パネル)、`ChipAreaScreenBounds`(`FanPopup.cs:63`)、`ChipTheme` / `GetChipColor`(チップ UI は廃止済み、現在は「行」)。AGENTS.md も「FanPopup」をファイル名として案内している。
- **なぜ負債か**: 新規読者がコードと見た目を対応づけにくい。ただし実害は小さい。
- **影響範囲**: リネームするとファイル名・クラス名・AGENTS.md に波及。**`ChipTheme` は JSON プロパティ名なので変更禁止**(変えるなら `[JsonPropertyName("ChipTheme")]` 必須だが、それ自体がリスク)。
- **変更リスク**: 中(機械的だが範囲が広く、diff ノイズが大きい)。
- **改善案**: やるなら `FanPopup` → `ListPopup` 等への一括リネーム+AGENTS.md 更新。JSON に関わる `ChipTheme` プロパティは温存。
- **検証**: ビルド+全スモーク。
- **実装可否**: **提案のみ**(Q5 でユーザーの意向を確認してから)。

### D7. `cmd /c` への文字列組み立ての脆さ
- **根拠**: `Launcher.cs:32,41` — `editor`(settings.json の自由文字列)が未クォートで cmd コマンドラインに埋め込まれる。パスは `QuoteCmdArgument`(`"` → `""`)でクォートされるが、cmd はクォート内でも `%` を環境変数展開する。
- **なぜ負債か**: ローカル設定ファイル由来なので攻撃境界としては低リスクだが、`%` を含むフォルダ名で誤動作する。`Editor` の契約(3種限定か自由値か)もコメント(`AppConfig.cs:81`)と実装(`EditorLabel` のフォールバック `Launcher.cs:14-19`)で曖昧。
- **影響範囲**: 起動コマンド。**Behaviors To Preserve #2 に直結**。
- **変更リスク**: 高(クォート方式の変更はあらゆる起動経路に波及)。
- **改善案**: 現状維持を基本とし、`%` 対策(`%` → `%%` ではなく cmd を介さない起動方式の検討)や `Editor` 値の検証は設計案として提示のみ。
- **検証**: —
- **実装可否**: **提案のみ**(Q4 の回答が前提)。

### D8. 小粒の整理(任意)
- **根拠**: ① `FanPopup._frame`(Bitmap)と `_anim`(Timer)が Form 破棄時に Dispose されない(`FanPopup.cs:53,51`、終了時のみの軽微リーク)。② DPI スケール計算が `MainForm.cs:29-31` と `FanPopup.cs:111` で重複。③ `PathEntry.DisplayName => Name`(`AppConfig.cs:13`)は実質パススルー(使用箇所: `ToString` と `MainForm.cs:210`)。
- **なぜ負債か**: いずれも軽微。③は「表示名 ≠ Name になる将来」を見込んだ抽象だが現状は無意味な間接化。
- **改善案**: ① `Dispose(bool)` override で破棄(安全・実装可)。②③は触らなくてよい(③の削除は使用箇所の書き換えを伴うわりに利得がない。残すこと自体を許容する)。
- **実装可否**: ①のみ**今実装してよい**。②③は**やらない**(記録のみ)。

## Implementation Phases

> 各フェーズは独立にコミットし、フェーズ完了ごとに Verification Requirements を実施。途中で Stop And Ask 条件に触れたら停止。

- **Phase 0 — 現状確認**: `git status`(未コミット差分があれば報告して停止)→ Baseline Commands 実行 → 結果を `.docs/refactor/memo.md` に追記。
- **Phase 1 — 安全網(D1)**: テストプロジェクト追加+ csproj の `tests/**` 除外 + 純粋関数4つの characterization test。`dotnet run` が従来どおり動くことを必ず確認。
- **Phase 2 — ドキュメント整合(D5 の①③)**: AGENTS.md の Launcher 記述更新。README は Q1 回答待ちのため触らない。
- **Phase 3 — 安全な補強(D2①② / D3 / D8①)**: `WaitForExit` 戻り値チェック+ディストリ名の static キャッシュ、壊れ settings.json の .bak 退避、FanPopup の Dispose。いずれも既存挙動の外側に防御を足すだけの変更に留める。
- **Phase 4 — 小さな責務分離(D4)**: テーマ系 static メソッドを `ChipThemes.cs` へ移動、参照差し替え。JSON 契約は不変。
- **Phase 5 — 提案書作成(D2③ / D6 / D7 / D3 の catch 絞り込み)**: 実装せず、`.docs/refactor/proposals.md` に設計案と影響範囲をまとめて終了。承認なしに実装しない。

## Verification Requirements

- 各フェーズ後: `dotnet build --nologo -v q`(警告0維持)、Phase 1 以降は `dotnet test tests` 成功。
- UI/起動系に触れたフェーズ(3, 4)後: `.\restart.ps1` で再起動し、手動スモーク①〜⑥を実施。
- settings.json を読む処理に触れたら: 既存の settings.json がそのまま読めること(エントリ消失なし)を起動して確認。
- 検証で1つでも落ちたら、そのフェーズの変更を revert してから原因を調査する。

## Reporting Format

作業完了時(または停止時)に以下を報告すること:

```
## 結果サマリ(フェーズごとに 完了/停止/スキップ と理由)
## 実行したコマンドと結果(baseline と各フェーズの検証。コピペで)
## 変更ファイル一覧(コミット単位)
## 手動スモークの実施結果(①〜⑥それぞれ)
## 停止した質問(あれば)
## memo.md への追記内容
```

## Out-of-scope Items

- UI デザイン・アニメーション・配色の変更(見た目は現状が正)。
- 新機能(エディタ追加、ホットキー、設定画面の刷新など)。
- net6.0 からのフレームワーク更新、NuGet 依存の追加(テスト用 xunit を除く)。
- `FanPopup` の描画方式(レイヤードウィンドウ/P/Invoke)の置き換え。
- `Push()` の HBITMAP 毎フレーム生成の最適化(実測の問題が出るまで触らない)。
- `publish/` `bin/` `obj/` 配下(生成物)。
- restart.ps1 の機能拡張。

---

## 実装前に確認すべき質問(人間が回答してから着手)

- **Q1(README)**: ba1a7f5 で README の「パスの種類」(WSL/Windows パスの変換説明)がスタートアップ登録手順に置き換わり、見出しと内容が不一致です。(a) パス種類の説明を復活させて「スタートアップ登録」を別見出しに分ける、(b) 見出しだけ「スタートアップ登録」に直す、どちらが意図ですか?
- **Q2(ChipTheme 旧エイリアス)**: `NormalizeChipTheme` の `DARK1`/`LIGHT2` 等は旧保存形式の互換とみられます。お使いの settings.json に旧形式の値が残っている可能性はありますか?(回答不能なら現状維持=削除しない、で進めます)
- **Q3(WslDistro の永続化)**: 既定ディストリ名を初回解決時に settings.json の `WslDistro` へ書き戻してよいですか?(保存データが変わります。No ならプロセス内キャッシュのみ)
- **Q4(Editor の契約)**: `Editor` は `code`/`cursor`/`antigravity` の3種限定ですか? それとも settings.json 手編集で任意のエディタ CLI を指定できることを意図していますか?(検証を入れるかどうかが変わります)
- **Q5(リネーム)**: `FanPopup` / `Chip*` 系の命名(扇型・チップ時代の名残)を現在の実態に合わせてリネームしたいですか?(JSON の `ChipTheme` は変更しません。diff が大きくなるため希望がなければ見送り)
