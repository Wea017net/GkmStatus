/*
 * Discord Social SDK 統合ガイド
 * 
 * このファイルは WinForms アプリケーションから Discord Rich Presence を使用する際の
 * 実装例を示しています。
 */

using System;
using System.Windows.Forms;
using GkmStatus.src.Discord;

namespace GkmStatus.Examples
{
    /// <summary>
    /// Discord Rich Presence 使用例（WinForms）
    /// </summary>
    public partial class DiscordPresenceExample : Form
    {
        private DiscordPresenceManager _discordManager;
        private const long DiscordClientId = 1352261574877778001;  // GkmStatus の Discord アプリ ID

        public DiscordPresenceExample()
        {
            InitializeComponent();
        }

        private void Form_Load(object sender, EventArgs e)
        {
            // Discord クライアントの初期化
            _discordManager = new DiscordPresenceManager(DiscordClientId);

            // イベントハンドラの登録
            _discordManager.InitializationSuccess += (s, e) =>
            {
                MessageBox.Show("Discord initialized successfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            _discordManager.InitializationFailed += (s, e) =>
            {
                MessageBox.Show($"Discord initialization failed: {e.GetException().Message}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            _discordManager.PresenceUpdated += (s, e) =>
            {
                // Presence 更新成功
            };

            // 初期化実行
            if (!_discordManager.Initialize())
            {
                MessageBox.Show("Failed to initialize Discord client", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Rich Presence を更新する例
        /// </summary>
        private void UpdatePresence_Example()
        {
            // 方法1: 直接引数で指定
            _discordManager.UpdatePresence(
                details: "Playing with Alice",
                detailsUrl: "https://example.com/game",
                state: "In Lobby",
                stateUrl: "https://example.com/lobby",
                largeImageKey: "gameplay",
                largeImageText: "Game Session",
                startTime: DateTime.UtcNow
            );

            // 方法2: ビルダーパターンを使用
            var presence = new RichPresenceBuilder()
                .WithDetails("Competing in Tournament")
                .WithDetailsUrl("https://example.com/tournament")
                .WithState("Round 3 - Top 8")
                .WithStateUrl("https://example.com/bracket")
                .WithLargeImage("tournament", "2024 Grand Championship")
                .WithSmallImage("rank_master", "Master Rank")
                .WithStartTime(DateTime.UtcNow)
                .WithParty("party_12345", 2, 4)
                .Build();

            // ビルダーで構築したデータを使用して更新
            _discordManager.UpdatePresence(
                presence.Details,
                presence.DetailsUrl,
                presence.State,
                presence.StateUrl,
                presence.LargeImageKey,
                presence.LargeImageText,
                presence.StartTime
            );
        }

        /// <summary>
        /// Presence をクリア（アクティビティを無くす）
        /// </summary>
        private void ClearPresence_Example()
        {
            _discordManager.ClearPresence();
        }

        private void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            // クリーンアップ
            _discordManager?.Dispose();
        }
    }

    /// <summary>
    /// 実装上の注意点
    /// 
    /// 1. DLL 配置:
    ///    - discord_social_sdk.dll をアプリケーション実行ファイルと同じディレクトリに配置してください
    ///    - または System PATH に追加してください
    ///    - ビルド設定で Output Directory にコピーするように設定してください（csproj）
    /// 
    /// 2. スレッド安全性:
    ///    - DiscordPresenceManager は内部的に Timer を使用してコールバックを処理しています
    ///    - UI スレッドをブロックしません
    ///    - 複数スレッドからの同時呼び出しは避けてください
    /// 
    /// 3. Discord アプリケーション ID:
    ///    - Discord Developer Portal で作成したアプリの ID を使用してください
    ///    - デモでは GkmStatus のアプリ ID を使用しています
    ///    - https://discord.com/developers/applications
    /// 
    /// 4. URL の形式:
    ///    - DetailsUrl, StateUrl は完全な http/https URL である必要があります
    ///    - ユーザーの Discord クライアント上で クリック可能になります
    /// 
    /// 5. テキスト長の制限:
    ///    - Details: 最大 128 文字
    ///    - State: 最大 128 文字
    ///    - DetailsUrl, StateUrl: 最大 256 文字
    /// 
    /// 6. 画像アセット:
    ///    - LargeImageKey, SmallImageKey は Discord Developer Portal で事前登録が必要です
    ///    - デフォルトキー "app" が利用可能です
    /// 
    /// 7. エラー処理:
    ///    - 各イベント（InitializationFailed, UpdateFailed）でエラーをハンドルしてください
    ///    - DLL が見つからない場合は DllNotFoundException が発生します
    /// 
    /// 8. リソース管理:
    ///    - フォーム終了時に必ず Dispose() を呼び出してください
    ///    - using ステートメントで自動クリーンアップすることも可能です
    /// 
    /// 9. ビルド設定（.csproj に追加）:
    ///    <ItemGroup>
    ///        <Content Include="path/to/discord_social_sdk.dll">
    ///            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    ///        </Content>
    ///    </ItemGroup>
    /// </summary>
}
