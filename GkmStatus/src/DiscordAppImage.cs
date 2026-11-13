using System.Net.Http;
using System.Text.Json;

namespace GkmStatus.src
{
    /// <summary>
    /// Discord Application に登録された画像（Rich Presence の Art Asset）を取得する。
    /// アプリに画像を同梱しないため、プレビューを開いたときに Discord の公開 API から読み込む。
    /// 一度読んだ画像は Application ID ごとに覚えておき、同じ画像を何度も取りに行かない。
    /// </summary>
    public static class DiscordAppImage
    {
        private static readonly HttpClient Client = CreateClient();
        private static readonly Dictionary<string, Task<Image?>> Cache = [];
        private static readonly object Gate = new();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConstants.APP_NAME);
            return client;
        }

        /// <summary>画像を取得する。取得できなかったときは null（次に呼んだときに取り直す）。</summary>
        public static Task<Image?> GetAsync(string appId, string assetName)
        {
            string key = appId + "/" + assetName;
            lock (Gate)
            {
                if (Cache.TryGetValue(key, out var cached) && !(cached.IsCompleted && cached.Result is null))
                    return cached;
                var task = LoadAsync(appId, assetName);
                Cache[key] = task;
                return task;
            }
        }

        private static async Task<Image?> LoadAsync(string appId, string assetName)
        {
            try
            {
                // 登録されている Asset の一覧から、名前（LargeImageKey）が一致するものの ID を探す
                using var list = await Client.GetStreamAsync($"https://discord.com/api/v10/oauth2/applications/{appId}/assets").ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(list).ConfigureAwait(false);
                string? assetId = document.RootElement.EnumerateArray()
                    .Where(asset => asset.TryGetProperty("name", out var name) && name.GetString() == assetName)
                    .Select(asset => asset.GetProperty("id").GetString())
                    .FirstOrDefault();
                if (assetId is null)
                {
                    ErrorLog.Write("Preview image", $"Asset '{assetName}' was not found in application {appId}.");
                    return null;
                }

                byte[] bytes = await Client.GetByteArrayAsync($"https://cdn.discordapp.com/app-assets/{appId}/{assetId}.png?size=160").ConfigureAwait(false);
                using var stream = new MemoryStream(bytes);
                using var decoded = Image.FromStream(stream);
                // Image.FromStream は元のストリームを使い続けるので、独立した Bitmap に写してから返す
                return new Bitmap(decoded);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Preview image", ex);
                return null;
            }
        }
    }
}
