# GkmStatus Ver.1.2.0 リリースノート（案）

> 公開前のレビュー用ドラフトです。機能修正PRの取り込みと最終検証後に、GitHub Releaseの説明へ転記してください。

## 主な変更

- アプリ内から最新のWindows x64版をダウンロードして更新できるようになりました。更新ファイルの検証後、確認ダイアログを経て置き換えます。
- システムトレイ格納中にAlt+Tabへウィンドウが残る問題と、復元後にタスクバーアイコンが既定表示になる問題を修正しました。
- ダーク/OLEDテーマで通常起動時にタイトルバーが白くなる問題を修正しました。
- 接続、設定の読み込み・保存、更新確認、画面状態の扱いを改善し、複数の不具合を修正しました。
- 初回起動時の「起動時に接続を試行」をオフにしました。保存済みの設定値は引き続き尊重します。

## ダウンロード

- Windows x64版（.NET Runtime 10.0同梱）: `GkmStatus_win-x64_v1.2.0.zip`

Ver.1.2.0以降はLite版の配布を終了します。Lite版をご利用中の方は通常版をダウンロードしてください。設定はそのまま引き継がれます。

## English

### Highlights

- Added in-app download and installation of the latest Windows x64 release, with package verification and user confirmation.
- Fixed the window remaining in Alt+Tab while minimized to the system tray and the default taskbar icon after restoring it.
- Fixed the white title bar when starting normally with the Dark/OLED theme.
- Improved connection handling, configuration loading and saving, update checks, and window state handling.
- Disabled “Connect on startup” by default for new configurations. Existing saved preferences remain unchanged.

### Downloads

- Windows x64 (includes .NET Runtime 10.0): `GkmStatus_win-x64_v1.2.0.zip`

The Lite edition is discontinued starting with v1.2.0. If you use the Lite edition, download the Standard edition; your existing settings will be preserved.
