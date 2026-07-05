using WindowSwitch.Services;

namespace WindowSwitch.Tests;

public sealed class HotkeyStatusComposerTests
{
    [Fact]
    public void Compose_WhenNoHotkeysRegisteredOrFailed_ReturnsEmpty()
    {
        var result = HotkeyStatusComposer.Compose([], []);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Compose_WhenOnlyRegistered_ReturnsAvailableMessage()
    {
        var result = HotkeyStatusComposer.Compose(
            registered: ["Ctrl + Alt + Space（按住滑动选择）"],
            failed: []);

        Assert.Equal("快捷键可用：Ctrl + Alt + Space（按住滑动选择）", result);
    }

    [Fact]
    public void Compose_WhenMultipleRegistered_JoinsWithSemicolon()
    {
        var result = HotkeyStatusComposer.Compose(
            registered: ["Ctrl + Alt + Space（按住滑动选择）", "Ctrl + Alt + 数字（0=第10桌面，支持两位）"],
            failed: []);

        Assert.Equal("快捷键可用：Ctrl + Alt + Space（按住滑动选择）；Ctrl + Alt + 数字（0=第10桌面，支持两位）", result);
    }

    [Fact]
    public void Compose_WhenOnlyFailed_ReturnsConflictMessage()
    {
        var result = HotkeyStatusComposer.Compose(
            registered: [],
            failed: ["Ctrl + Alt + Space"]);

        Assert.Equal("快捷键冲突：Ctrl + Alt + Space 已被其他应用占用或被系统保留。", result);
    }

    [Fact]
    public void Compose_WhenMultipleFailed_JoinsWithComma()
    {
        var result = HotkeyStatusComposer.Compose(
            registered: [],
            failed: ["Ctrl + Alt + Space", "Ctrl + Alt + 1"]);

        Assert.Equal("快捷键冲突：Ctrl + Alt + Space, Ctrl + Alt + 1 已被其他应用占用或被系统保留。", result);
    }

    [Fact]
    public void Compose_WhenBothRegisteredAndFailed_ConflictMessageTakesPrecedence()
    {
        // 只要有失败，就报告冲突（不论是否有部分注册成功）
        var result = HotkeyStatusComposer.Compose(
            registered: ["Ctrl + Alt + 数字（0=第10桌面，支持两位）"],
            failed: ["Ctrl + Alt + Space"]);

        Assert.Equal("快捷键冲突：Ctrl + Alt + Space 已被其他应用占用或被系统保留。", result);
    }
}
