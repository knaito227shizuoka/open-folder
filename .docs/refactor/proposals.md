# リファクタリング提案(承認待ち・未実装)

`refactor-instructions.md` の Phase 5 に基づく。ここに挙げた項目は**実装していない**。
プロダクト判断・互換性・大きな diff を伴うため、人間の承認を得てから着手すること。
各項目に対応する Debt Map ID と確認すべき質問(Q番号)を併記する。

---

## P1. WSL ディストリ名の settings.json への永続化(D2③ / Q3)

- **現状**: `Launcher.GetDefaultWslDistro` の結果は Phase 3 でプロセス内 static にキャッシュ済み。ただしアプリ再起動のたびに再取得が走る。
- **提案**: 初回解決した既定ディストリ名を `PathEntry.WslDistro`(または新フィールド)へ書き戻し、settings.json に永続化する。再起動後も `wsl` 呼び出しを避けられる。
- **なぜ承認が必要か**: **保存データの内容が変わる**(Stop And Ask 条件)。ユーザーが既定ディストリを切り替えた場合に古い値が残る副作用もある。
- **影響範囲**: `AppConfig`(保存内容)、`Launcher.Open`、既存 settings.json の互換。
- **リスク**: 中。書き戻しのタイミング(起動時 vs 起動成功時)とディストリ変更時の無効化方針を決める必要あり。
- **検証案**: WSL エントリ起動 → settings.json に `WslDistro` が入る → 2回目以降 `wsl` プロセスが起動しないことを確認。
- **判断材料(Q3)**: 既定ディストリ名を settings.json に書き戻してよいか? No ならプロセス内キャッシュ(実装済み)で十分。

## P2. `GetDefaultWslDistro` の完全非同期化(D2 派生)

- **現状**: Phase 3 で `WaitForExit(3000)` を先に呼びタイムアウトを実効化し、出力が短い前提で安全化済み。ただし呼び出し自体は UI スレッドで同期実行(最大3秒ブロックし得る)。
- **提案**: `Launcher.Open` を async 化し、ディストリ取得をバックグラウンドで行って UI を固めない。あるいは起動を `Task.Run` でオフロードし、失敗時のみ UI スレッドへ戻して MessageBox。
- **なぜ承認が必要か**: `Launcher.Open` の呼び出し規約変更が `MainForm.OpenEntry`(`MainForm.cs:288-299`)へ波及し、影響が広い。指示書で async 化は提案止まりと指定。
- **影響範囲**: `Launcher`、`MainForm`、例外ハンドリング経路。
- **リスク**: 中〜高。UI スレッドマーシャリングの考慮が必要。
- **検証案**: WSL コールドスタート時にウィジェットがフリーズしないこと、起動失敗時に従来どおり MessageBox が出ることを手動確認。

## P3. `AppConfig.Load` の catch 範囲を絞る(D3 派生)

- **現状**: Phase 3 で「読めない場合は .bak 退避してから初期化」を追加。ただし catch は依然として全例外を捕捉する。
- **提案**: `catch (JsonException)` / `catch (IOException)` に絞り、想定外の例外(プログラミングエラー等)は握りつぶさず表面化させる。
- **なぜ承認が必要か**: 握りつぶし範囲を狭めると、これまで黙って初期化されていたケースで例外が表に出る=起動時クラッシュに変わり得る(挙動変化)。
- **影響範囲**: `AppConfig.Load` のみ。
- **リスク**: 低〜中。例外種別の洗い出しが要る。
- **検証案**: 壊れ JSON・権限エラー・存在しないドライブ等で起動挙動を確認。

## P4. `cmd /c` を介さない起動 / `editor` 値の検証(D7 / Q4)

- **現状**: `Launcher.Open` は `cmd /c <editor> ...` で起動。パスは `QuoteCmdArgument` でクォートするが、(a) `editor`(settings.json の自由文字列)は未クォート、(b) cmd はクォート内でも `%` を環境変数展開する。
- **提案**:
  - (a) `editor` を既知の許可リスト(`code`/`cursor`/`antigravity`)で検証、または同様にクォート。
  - (b) `%` を含むパスの誤動作を避けるなら、`.cmd` シムを直接 `ProcessStartInfo`(`FileName` にシムのフルパス、`ArgumentList` で引数)で起動し cmd を介さない方式を検討。ただしシムの解決(PATH 探索)を自前で行う必要がある。
- **なぜ承認が必要か**: **起動コマンドの形は Behaviors To Preserve #2**。クォート方式や起動経路の変更は全エディタ・全パス種別に波及する高リスク変更。
- **影響範囲**: `Launcher.Open`(WSL/Windows 両経路)。
- **リスク**: 高。
- **判断材料(Q4)**: `Editor` は3種限定か、手編集で任意 CLI を許す意図か? 限定なら許可リスト検証が素直。

## P5. 命名の現代化リネーム(D6 / Q5)

- **現状**: `FanPopup`(現在は扇型でなく縦パネル)、`ChipAreaScreenBounds`、`Chip*`(チップ UI は廃止、現在は「行」)など、過去の UI 形態の名残が残る。
- **提案**: `FanPopup` → `ListPopup` 等、実態に合わせて一括リネーム。`ChipThemes`/`ChipTheme` のうち **JSON プロパティ `PathEntry.ChipTheme` は変更しない**(保存互換のため。変えるなら `[JsonPropertyName("ChipTheme")]` 必須でリスクが増す)。AGENTS.md も追従。
- **なぜ承認が必要か**: 機能的価値はなく diff が大きい。ユーザーの意向次第。
- **影響範囲**: ファイル名・クラス名・AGENTS.md。JSON 契約は不変。
- **リスク**: 中(機械的だがノイズ大)。
- **判断材料(Q5)**: リネームを希望するか? 希望がなければ見送り推奨。

## P6. xunit への移行(オフライン制約の解消後)

- **現状**: オフラインで test SDK が復元できず、依存ゼロの自前コンソールランナー(`dotnet run --project tests`)を使用。
- **提案**: ネットワーク復元が可能になったら xunit + `Microsoft.NET.Test.Sdk` へ移行し、`dotnet test` に統一。テストケースの期待値はそのまま流用できる。
- **リスク**: 低。
- **備考**: 現ランナーでも退行検出の目的は達成しているため、優先度は低い。

---

## 参考: 触らないと決めた軽微項目(記録のみ)

- `PathEntry.DisplayName => Name`(`AppConfig.cs`)のパススルー間接化。削除しても利得が小さく、使用箇所(`ToString`/`MainForm`)の書き換えコストに見合わないため現状維持。
- DPI スケール計算が `MainForm` と `FanPopup` で重複。共通化の利得が小さく、座標系も別物のため現状維持。
- `FanPopup.Push()` の毎フレーム HBITMAP 生成。パネルが小さく実測問題が出ていないため、計測してから対応(Out-of-scope)。
