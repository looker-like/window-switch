namespace WindowSwitch.Services;

public static class HotkeyStatusComposer
{
    public static string Compose(IReadOnlyList<string> registered, IReadOnlyList<string> failed)
    {
        if (failed.Count > 0)
        {
            return $"快捷键冲突：{string.Join(", ", failed)} 已被其他应用占用或被系统保留。";
        }

        if (registered.Count > 0)
        {
            return $"快捷键可用：{string.Join("；", registered)}";
        }

        return string.Empty;
    }
}

