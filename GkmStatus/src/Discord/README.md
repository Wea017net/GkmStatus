# Discord Social SDK 統合ガイド

## 概要

このドキュメントでは、GkmStatus に Discord Social SDK (Native C API) を P/Invoke で統合する方法を説明します。

## ファイル構成

```
GkmStatus/src/Discord/
├── NativeInterop.cs              # P/Invoke 定義と構造体
├── DiscordPresenceManager.cs     # WinForms 向けマネージャークラス
├── DiscordModels.cs              # C# 側のモデルクラス
├── DiscordPresenceExample.cs     # 使用例とガイド
└── README.md                     # このファイル
```

## 主要機能

### 1. NativeInterop.cs

Discord Social SDK のネイティブ API を P/Invoke 経由で呼び出すための定義を含みます。

**主要な構造体:**
- `DiscordActivity`: Rich Presence の情報を格納
  - `Details` / `DetailsUrl`: 詳細テキストとそのクリック時のURL
  - `State` / `StateUrl`: 状態テキストとそのクリック時のURL
  - `Timestamps`: 開始/終了時刻
  - `Assets`: 画像情報
  - `Party`: マルチプレイ情報

**主要な P/Invoke 関数:**
- `DiscordCreate()`: クライアント初期化
- `ActivityManager_UpdateActivity()`: Rich Presence 更新
- `DiscordRunCallbacks()`: コールバック処理
- `DiscordDestroy()`: クリーンアップ

### 2. DiscordPresenceManager.cs

WinForms アプリケーション向けの高レベルマネージャークラスです。

**特徴:**
- UI スレッドをブロックしないよう Timer を使用
- イベントベースのエラーハンドリング
- 自動リソース管理 (IDisposable)

**使用例:**

```csharp
// 初期化
var discordManager = new DiscordPresenceManager(clientId: 1352261574877778001);
discordManager.Initialize();

// Presence 更新
discordManager.UpdatePresence(
    details: "Playing with Alice",
    detailsUrl: "https://example.com/game",
    state: "In Lobby",
    stateUrl: "https://example.com/lobby"
);

// クリーンアップ
discordManager.Dispose();
```

### 3. DiscordModels.cs

マネージド側のモデルクラスとビルダーパターンを提供します。

**RichPresenceBuilder:**

```csharp
var presence = new RichPresenceBuilder()
    .WithDetails("Competing in Tournament")
    .WithDetailsUrl("https://example.com/tournament")
    .WithState("Round 3 - Top 8")
    .WithStateUrl("https://example.com/bracket")
    .WithLargeImage("tournament", "2024 Grand Championship")
    .Build();
```

## セットアップ手順

### 1. Discord Social SDK DLL の取得

Discord Developer Portal から Social SDK をダウンロードし、`discord_social_sdk.dll` を入手してください。

- **URL**: https://discord.com/developers
- 作成したアプリケーション → OAuth2 → Social SDK セクション

### 2. DLL の配置

#### 方法A: 出力ディレクトリに配置（推奨）

`.csproj` ファイルに以下を追加：

```xml
<ItemGroup>
    <Content Include="path/to/discord_social_sdk.dll">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>
```

#### 方法B: 手動配置

`discord_social_sdk.dll` をビルド出力ディレクトリ（例：`bin/Debug/`) に配置してください。

### 3. プロジェクト参照

コンパイル時に追加の設定は不要です。P/Invoke 定義はすべて `NativeInterop.cs` に含まれています。

## 実装例

### 基本的な使用方法

```csharp
using GkmStatus.src.Discord;

public partial class MainForm : Form
{
    private DiscordPresenceManager _discordManager;
    private const long ClientId = 1352261574877778001;

    public MainForm()
    {
        InitializeComponent();
    }

    private void Form_Load(object sender, EventArgs e)
    {
        // マネージャーの作成と初期化
        _discordManager = new DiscordPresenceManager(ClientId);
        
        if (_discordManager.Initialize())
        {
            // 初期化成功
            UpdatePresence();
        }
        else
        {
            MessageBox.Show("Failed to initialize Discord");
        }
    }

    private void UpdatePresence()
    {
        _discordManager.UpdatePresence(
            details: "Playing a game",
            detailsUrl: "https://example.com/game",
            state: "Level 10",
            stateUrl: "https://example.com/progress",
            largeImageKey: "gameplay",
            largeImageText: "In Game"
        );
    }

    private void Form_FormClosing(object sender, FormClosingEventArgs e)
    {
        _discordManager?.Dispose();
    }
}
```

### ビルダーパターンでの構築

```csharp
var presence = new RichPresenceBuilder()
    .WithDetails("Editing Configuration")
    .WithDetailsUrl("https://example.com/config")
    .WithState("Settings Menu")
    .WithStateUrl("https://example.com/settings")
    .WithLargeImage("settings_icon", "Configuration")
    .WithSmallImage("version_badge", "v1.0")
    .WithStartTime(DateTime.UtcNow)
    .Build();

_discordManager.UpdatePresence(
    presence.Details,
    presence.DetailsUrl,
    presence.State,
    presence.StateUrl
);
```

## テキスト長の制限

Discord Rich Presence には以下の長さ制限があります：

| フィールド | 最大文字数 |
|-----------|---------|
| Details | 128 |
| State | 128 |
| DetailsUrl | 256 |
| StateUrl | 256 |
| LargeImageKey | 128 |
| SmallImageKey | 128 |

長すぎるテキストは自動的に切り詰められます。

## 画像アセット設定

大画像、小画像のキーを使用するには、事前に Discord Developer Portal で登録が必要です：

1. アプリケーション → Rich Presence → Asset Upload
2. 画像をアップロード
3. 画像キーを指定

デフォルトで `"app"` キーが利用可能です（GkmStatus ロゴ）。

## エラーハンドリング

### イベントベースのエラーハンドリング

```csharp
_discordManager.InitializationFailed += (s, e) =>
{
    var exception = e.GetException();
    if (exception is DllNotFoundException)
    {
        MessageBox.Show("Discord SDK DLL not found");
    }
    else
    {
        MessageBox.Show($"Error: {exception.Message}");
    }
};

_discordManager.UpdateFailed += (s, e) =>
{
    Debug.WriteLine($"Failed to update presence: {e.GetException().Message}");
};
```

## トラブルシューティング

### DllNotFoundException

**原因**: `discord_social_sdk.dll` が見つからない

**解決策**:
1. DLL がアプリケーション実行ファイルと同じディレクトリにあるか確認
2. `.csproj` の `CopyToOutputDirectory` 設定を確認
3. ビルドを再実行して DLL がコピーされているか確認

### クライアント初期化失敗

**原因**: Discord Social SDK の初期化に失敗

**解決策**:
1. Client ID が正しいか確認
2. Discord Developer Portal でアプリケーションが作成されているか確認
3. ディスクの空き容量やシステムリソースを確認

### Presence が更新されない

**原因**: 多くの場合、キャッシュまたはコールバック処理の遅延

**解決策**:
1. `DiscordRunCallbacks()` が定期的に呼ばれているか確認（Timer で自動実行）
2. 十分な時間を待つ（数秒）
3. Discord クライアントが起動しているか確認

## スレッド安全性

`DiscordPresenceManager` は以下の点で安全です：

- 内部的に Timer でコールバック処理を実行
- 複数スレッドからの同時呼び出しは避けてください
- UI スレッドから呼び出すことを推奨

## パフォーマンス考慮事項

- `DiscordRunCallbacks()` は 100ms 間隔で実行されます（設定可能）
- Presence 更新の頻度を高すぎないようにしてください（秒単位が目安）
- Large Image は可能な限り小さいファイルサイズで登録してください

## ビルド設定テンプレート

`.csproj` に追加するテンプレート：

```xml
<ItemGroup>
    <!-- Discord Social SDK DLL を出力ディレクトリにコピー -->
    <Content Include="path/to/discord_social_sdk.dll">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>

<PropertyGroup>
    <!-- 64ビット実行可能ファイルをサポート（推奨） -->
    <PlatformTarget>x64</PlatformTarget>
</PropertyGroup>
```

## ライセンスと利用規約

Discord Social SDK の使用には Discord 利用規約が適用されます。
詳細は https://discord.com/developers/docs を参照してください。

## サポートとドキュメント

- Discord Developer Portal: https://discord.com/developers
- Discord API Docs: https://discord.com/developers/docs/rich-presence/how-to
- GkmStatus GitHub: https://github.com/Wea017net/GkmStatus
