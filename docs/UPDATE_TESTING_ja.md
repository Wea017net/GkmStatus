# 自動アップデートの隔離テスト

このテストは、テスト用にコンパイルした2つのアプリ版とローカルZIPを使います。GitHub Releaseへテスト版を公開せず、通常の`%APPDATA%\GkmStatus`や既存のアプリ配置先にも触れません。更新時のUAC確認は実際に表示されます。

PowerShellでリポジトリのルートから実行します。

```powershell
.\scripts\Prepare-UpdateTestEnvironment.ps1
```

スクリプトはWindows x64の単一ファイル版を2つ発行し、更新前の`0.1.0`を起動用フォルダーへ、更新後の`99.0.0`をローカルRelease ZIPへ配置します。生成物は`%TEMP%\GkmStatusUpdaterTest\<実行日時>`の下に保存されます。ビルド後、テストアプリは次のコマンドで起動します。

```powershell
.\scripts\Prepare-UpdateTestEnvironment.ps1 -Launch
```

起動後、トレイに出る更新通知をクリックするか、アプリの「今すぐ更新を確認」から更新を実行します。UACを承認するとUpdaterがテスト用ZIPを取得し、進捗画面の後にアプリを置き換えて再起動します。再起動後に「今すぐ更新を確認」を実行し、最新バージョンと判定されることを確認します。

テスト用プロファイルは同じ実行日時フォルダー内の`profile`です。`config.json`、Updater、ダウンロードはここに保存されます。昇格後の展開とバックアップは、一般ユーザーから書き換えられない`%ProgramData%\GkmStatus\UpdaterWork`に一時作成され、完了後に削除されます。既存の設定やアプリには触れません。スクリプトは実行ごとに別のフォルダーを作り、自動削除はしません。

このテストビルドだけに有効なローカルRelease機能は`GkmStatusUpdaterTestMode=true`でコンパイルされます。通常のReleaseビルドではローカルファイルを更新元として受け付けません。
