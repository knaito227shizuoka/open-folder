# open-folder

デスクトップに常駐する小型ランチャーウィジェット。登録したプロジェクトフォルダをワンクリックで VS Code / Cursor / Antigravity で開けます。WSL 内パスにも対応。

## 起動

```
dotnet run
```

## 使い方

| 操作 | 動作 |
|---|---|
| 左クリック | 登録パス一覧を表示 → 選択でエディタ起動 |
| 右クリック | 設定メニュー(パス追加・管理 / エディタ切替 / 常に手前に表示 / 終了) |
| 左ドラッグ | ウィジェットを移動(位置は保存される) |

## パスの種類

- **WSL パス** (`/home/user/project` など) → `wsl -e bash -lc "code '/home/user/project'"` で起動
- **Windows パス** (`C:\project` など) → `code "C:\project"` で起動
- `\\wsl.localhost\Ubuntu\home\...` 形式を入力すると自動で WSL パスに変換されます

## 設定ファイル

`%APPDATA%\open-folder\settings.json` に保存されます(登録パス・エディタ・最前面設定・ウィンドウ位置)。
