namespace GkmStatus.src;

public static class PresenceEditorHistory
{
    public static void SaveEditorHistory(
        PresenceSettings presence,
        PresenceStateType stateType,
        string? stateText,
        PresenceButtonMode buttonMode,
        string? label1,
        string? url1,
        string? label2,
        string? url2)
    {
        if (stateType is PresenceStateType.PID or PresenceStateType.Custom)
            presence.StateHistory[AppSettingsHelper.GetStateString(stateType)] = stateText ?? string.Empty;

        if (buttonMode == PresenceButtonMode.Custom)
        {
            presence.ButtonHistory[AppSettingsHelper.GetButtonModeString(buttonMode)] = new ButtonHistoryData
            {
                L1 = label1 ?? string.Empty,
                U1 = url1 ?? string.Empty,
                L2 = label2 ?? string.Empty,
                U2 = url2 ?? string.Empty
            };
        }
    }
}
