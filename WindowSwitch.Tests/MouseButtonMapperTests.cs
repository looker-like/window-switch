using WindowSwitch.Services;

namespace WindowSwitch.Tests;

public sealed class MouseButtonMapperTests
{
    // ── 右键 ──────────────────────────────────────────────────────────
    [Fact]
    public void TryMap_RightButtonDown_ReturnsRightIsDown()
    {
        var result = MouseButtonMapper.TryMap(0x0204, mouseData: 0,
            out var button, out var isDown, out var isUp);

        Assert.True(result);
        Assert.Equal(MouseHotkeyButton.Right, button);
        Assert.True(isDown);
        Assert.False(isUp);
    }

    [Fact]
    public void TryMap_RightButtonUp_ReturnsRightIsUp()
    {
        var result = MouseButtonMapper.TryMap(0x0205, mouseData: 0,
            out var button, out var isDown, out var isUp);

        Assert.True(result);
        Assert.Equal(MouseHotkeyButton.Right, button);
        Assert.False(isDown);
        Assert.True(isUp);
    }

    // ── 中键 ──────────────────────────────────────────────────────────
    [Fact]
    public void TryMap_MiddleButtonDown_ReturnsMiddleIsDown()
    {
        var result = MouseButtonMapper.TryMap(0x0207, mouseData: 0,
            out var button, out var isDown, out var isUp);

        Assert.True(result);
        Assert.Equal(MouseHotkeyButton.Middle, button);
        Assert.True(isDown);
        Assert.False(isUp);
    }

    [Fact]
    public void TryMap_MiddleButtonUp_ReturnsMiddleIsUp()
    {
        var result = MouseButtonMapper.TryMap(0x0208, mouseData: 0,
            out var button, out var isDown, out var isUp);

        Assert.True(result);
        Assert.Equal(MouseHotkeyButton.Middle, button);
        Assert.False(isDown);
        Assert.True(isUp);
    }

    // ── XButton1 ──────────────────────────────────────────────────────
    [Fact]
    public void TryMap_XButton1Down_ReturnsXButton1IsDown()
    {
        // mouseData 高 16 位 = 0x0001 → XButton1
        var mouseData = 0x0001 << 16;

        var result = MouseButtonMapper.TryMap(0x020B, mouseData,
            out var button, out var isDown, out var isUp);

        Assert.True(result);
        Assert.Equal(MouseHotkeyButton.XButton1, button);
        Assert.True(isDown);
        Assert.False(isUp);
    }

    [Fact]
    public void TryMap_XButton1Up_ReturnsXButton1IsUp()
    {
        var mouseData = 0x0001 << 16;

        var result = MouseButtonMapper.TryMap(0x020C, mouseData,
            out var button, out var isDown, out var isUp);

        Assert.True(result);
        Assert.Equal(MouseHotkeyButton.XButton1, button);
        Assert.False(isDown);
        Assert.True(isUp);
    }

    // ── XButton2 ──────────────────────────────────────────────────────
    [Fact]
    public void TryMap_XButton2Down_ReturnsXButton2IsDown()
    {
        // mouseData 高 16 位 = 0x0002 → XButton2
        var mouseData = 0x0002 << 16;

        var result = MouseButtonMapper.TryMap(0x020B, mouseData,
            out var button, out var isDown, out var isUp);

        Assert.True(result);
        Assert.Equal(MouseHotkeyButton.XButton2, button);
        Assert.True(isDown);
        Assert.False(isUp);
    }

    // ── 其他消息返回 false ────────────────────────────────────────────
    [Fact]
    public void TryMap_MouseMove_ReturnsFalse()
    {
        var result = MouseButtonMapper.TryMap(0x0200, mouseData: 0,
            out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryMap_LeftButtonDown_ReturnsFalse()
    {
        // 左键不在支持范围内（由调用方的 IsCapturing 路径处理）
        var result = MouseButtonMapper.TryMap(0x0201, mouseData: 0,
            out _, out _, out _);

        Assert.False(result);
    }
}
