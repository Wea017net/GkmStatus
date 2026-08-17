using System;

namespace GkmStatus.src.Discord
{
    /// <summary>
    /// Discord Rich Presence モデルクラス
    /// マネージド環境から使用するための高レベルインターフェース
    /// </summary>
    public class RichPresenceData
    {
        /// <summary>
        /// 詳細テキスト
        /// </summary>
        public string Details { get; set; } = "";

        /// <summary>
        /// Details をクリックしたときに開く URL
        /// </summary>
        public string DetailsUrl { get; set; } = "";

        /// <summary>
        /// 状態テキスト
        /// </summary>
        public string State { get; set; } = "";

        /// <summary>
        /// State をクリックしたときに開く URL
        /// </summary>
        public string StateUrl { get; set; } = "";

        /// <summary>
        /// 大画像キー
        /// </summary>
        public string LargeImageKey { get; set; } = "app";

        /// <summary>
        /// 大画像ホバーテキスト
        /// </summary>
        public string LargeImageText { get; set; } = "GkmStatus";

        /// <summary>
        /// 小画像キー（オプション）
        /// </summary>
        public string SmallImageKey { get; set; } = "";

        /// <summary>
        /// 小画像ホバーテキスト（オプション）
        /// </summary>
        public string SmallImageText { get; set; } = "";

        /// <summary>
        /// アクティビティ開始時刻
        /// </summary>
        public DateTime? StartTime { get; set; } = null;

        /// <summary>
        /// パーティ ID
        /// </summary>
        public string PartyId { get; set; } = "";

        /// <summary>
        /// パーティの現在メンバー数
        /// </summary>
        public int PartySize { get; set; } = 0;

        /// <summary>
        /// パーティの最大メンバー数
        /// </summary>
        public int PartyMax { get; set; } = 0;

        /// <summary>
        /// 現在のモデル情報をディープコピー
        /// </summary>
        public RichPresenceData Clone()
        {
            return new RichPresenceData
            {
                Details = this.Details,
                DetailsUrl = this.DetailsUrl,
                State = this.State,
                StateUrl = this.StateUrl,
                LargeImageKey = this.LargeImageKey,
                LargeImageText = this.LargeImageText,
                SmallImageKey = this.SmallImageKey,
                SmallImageText = this.SmallImageText,
                StartTime = this.StartTime,
                PartyId = this.PartyId,
                PartySize = this.PartySize,
                PartyMax = this.PartyMax
            };
        }
    }

    /// <summary>
    /// Discord Rich Presence ビルダー
    /// Fluent API でPresence を構築するためのヘルパークラス
    /// </summary>
    public class RichPresenceBuilder
    {
        private readonly RichPresenceData _data = new();

        /// <summary>
        /// 詳細テキストを設定
        /// </summary>
        public RichPresenceBuilder WithDetails(string details)
        {
            _data.Details = details ?? "";
            return this;
        }

        /// <summary>
        /// Details URL を設定
        /// </summary>
        public RichPresenceBuilder WithDetailsUrl(string url)
        {
            _data.DetailsUrl = url ?? "";
            return this;
        }

        /// <summary>
        /// 状態テキストを設定
        /// </summary>
        public RichPresenceBuilder WithState(string state)
        {
            _data.State = state ?? "";
            return this;
        }

        /// <summary>
        /// State URL を設定
        /// </summary>
        public RichPresenceBuilder WithStateUrl(string url)
        {
            _data.StateUrl = url ?? "";
            return this;
        }

        /// <summary>
        /// 大画像を設定
        /// </summary>
        public RichPresenceBuilder WithLargeImage(string key, string text = "")
        {
            _data.LargeImageKey = key ?? "";
            _data.LargeImageText = text ?? "";
            return this;
        }

        /// <summary>
        /// 小画像を設定
        /// </summary>
        public RichPresenceBuilder WithSmallImage(string key, string text = "")
        {
            _data.SmallImageKey = key ?? "";
            _data.SmallImageText = text ?? "";
            return this;
        }

        /// <summary>
        /// 開始時刻を設定
        /// </summary>
        public RichPresenceBuilder WithStartTime(DateTime startTime)
        {
            _data.StartTime = startTime;
            return this;
        }

        /// <summary>
        /// パーティ情報を設定
        /// </summary>
        public RichPresenceBuilder WithParty(string id, int current, int max)
        {
            _data.PartyId = id ?? "";
            _data.PartySize = current;
            _data.PartyMax = max;
            return this;
        }

        /// <summary>
        /// ビルド完了、RichPresenceData を返す
        /// </summary>
        public RichPresenceData Build()
        {
            return _data.Clone();
        }
    }
}
