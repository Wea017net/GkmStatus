namespace GkmStatus.src
{
    /// <summary>
    /// プロデューサーレベル（PLv）の見直しを促す通知の間隔を判定する。
    /// 長く使っているうちにゲーム内の PLv が上がり、アプリの PLv が古いままになるのを防ぐ。
    /// </summary>
    public static class ProducerLevelReminder
    {
        public static readonly TimeSpan Interval = TimeSpan.FromDays(30);

        /// <summary>
        /// この PLv 以上なら、設定にかかわらず通知しない（ゲームの PLv 上限に近く、上がる余地が小さいため）。
        /// ゲーム側で PLv の上限が上がったら、この値を変える。
        /// </summary>
        public const int StopLevel = 90;

        /// <summary>
        /// 起点の日時を記録し直す必要があるか。未記録（初回起動・旧バージョンからの更新）のときと、
        /// 時計の変更などで未来の日時になっているとき。
        /// </summary>
        public static bool NeedsStart(DateTime lastUtc, DateTime nowUtc)
            => lastUtc == DateTime.MinValue || lastUtc > nowUtc;

        /// <summary>前回の起点から Interval 以上たったか。</summary>
        public static bool IsDue(DateTime lastUtc, DateTime nowUtc)
            => !NeedsStart(lastUtc, nowUtc) && nowUtc - lastUtc >= Interval;

        /// <summary>この PLv なら通知を止めるか。</summary>
        public static bool IsAtStopLevel(int level) => level >= StopLevel;
    }
}
