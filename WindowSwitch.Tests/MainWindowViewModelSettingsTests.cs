using WindowSwitch.Models;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;

namespace WindowSwitch.Tests;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public void InitialSettingsAreApplied()
    {
        var fake = new FakeVirtualDesktopService();
        var settings = new WindowSettings(
            10,
            20,
            isFloatingTopmost: false,
            columnsPerRow: 3,
            windowOpacity: 0.75,
            startHidden: true,
            autoHideAfterSwitch: true,
            enableColoredDesktopLabels: true,
            isHotkeyEnabled: false,
            isDesktopHotkeysEnabled: false,
            showHotkeyModifiers: (int)(HotkeyModifiers.Control | HotkeyModifiers.Shift),
            showHotkeyVirtualKey: 0x71,
            desktopHotkeyModifiers: (int)(HotkeyModifiers.Alt | HotkeyModifiers.Shift),
            showHotkeyKind: (int)ShowHotkeyKind.Keyboard,
            showHotkeyMouseButton: (int)MouseHotkeyButton.XButton1);

        using var viewModel = new MainWindowViewModel(fake, settings);

        Assert.False(viewModel.IsFloatingTopmost);
        Assert.True(viewModel.StartHidden);
        Assert.True(viewModel.AutoHideAfterSwitch);
        Assert.True(viewModel.EnableColoredDesktopLabels);
        Assert.False(viewModel.IsHotkeyEnabled);
        Assert.False(viewModel.IsDesktopHotkeysEnabled);
        Assert.Equal((int)(HotkeyModifiers.Control | HotkeyModifiers.Shift), viewModel.ShowHotkeyModifiers);
        Assert.Equal(0x71, viewModel.ShowHotkeyVirtualKey);
        Assert.Equal(ShowHotkeyKind.Keyboard, viewModel.ShowHotkeyKind);
        Assert.Equal(MouseHotkeyButton.XButton1, viewModel.ShowHotkeyMouseButton);
        Assert.Equal("Ctrl + Shift + F2", viewModel.HotkeyText);
        Assert.Equal((int)(HotkeyModifiers.Alt | HotkeyModifiers.Shift), viewModel.DesktopHotkeyModifiers);
        Assert.Equal("Alt + Shift + 数字", viewModel.DesktopHotkeyText);
        Assert.Equal(3, viewModel.ColumnsPerRow);
        Assert.Equal(0.75, viewModel.WindowOpacity);
        Assert.Equal(75, viewModel.WindowOpacityPercent);
    }

    [Fact]
    public void SettingsAreClampedToSupportedRanges()
    {
        var fake = new FakeVirtualDesktopService();
        var settings = new WindowSettings(
            10,
            20,
            columnsPerRow: 99,
            windowOpacity: 0.1,
            showHotkeyModifiers: 0,
            showHotkeyVirtualKey: 0x31,
            desktopHotkeyModifiers: 0xFFFF,
            showHotkeyKind: 99,
            showHotkeyMouseButton: 99);

        using var viewModel = new MainWindowViewModel(fake, settings);

        Assert.Equal(4, viewModel.ColumnsPerRow);
        Assert.Equal(0.35, viewModel.WindowOpacity);
        Assert.Equal(35, viewModel.WindowOpacityPercent);
        Assert.False(viewModel.EnableColoredDesktopLabels);
        Assert.Equal(HotkeyDefinitions.DefaultHotkeyModifiers, viewModel.ShowHotkeyModifiers);
        Assert.Equal(0x31, viewModel.ShowHotkeyVirtualKey);
        Assert.Equal(HotkeyDefinitions.DefaultHotkeyModifiers, viewModel.DesktopHotkeyModifiers);
        Assert.Equal(HotkeyDefinitions.DefaultShowHotkeyKind, viewModel.ShowHotkeyKind);
        Assert.Equal(HotkeyDefinitions.DefaultShowHotkeyMouseButton, viewModel.ShowHotkeyMouseButton);
    }

    [Fact]
    public void LeftMouseButtonIsNotAcceptedAsConfiguredShowHotkey()
    {
        var fake = new FakeVirtualDesktopService();
        var settings = new WindowSettings(
            10,
            20,
            showHotkeyKind: (int)ShowHotkeyKind.MouseButton,
            showHotkeyMouseButton: (int)MouseHotkeyButton.Left);

        using var viewModel = new MainWindowViewModel(fake, settings);

        Assert.Equal(HotkeyDefinitions.DefaultShowHotkeyMouseButton, viewModel.ShowHotkeyMouseButton);
        Assert.Equal("鼠标中键", viewModel.HotkeyText);
    }

    [Fact]
    public void CapturedKeyboardHotkeyUpdatesDisplayText()
    {
        using var viewModel = new MainWindowViewModel(new FakeVirtualDesktopService());

        viewModel.ApplyCapturedShowHotkey(new CapturedShowHotkey(
            ShowHotkeyKind.Keyboard,
            (int)(HotkeyModifiers.Alt | HotkeyModifiers.Shift),
            0x31,
            MouseHotkeyButton.Middle));

        Assert.Equal(ShowHotkeyKind.Keyboard, viewModel.ShowHotkeyKind);
        Assert.Equal((int)(HotkeyModifiers.Alt | HotkeyModifiers.Shift), viewModel.ShowHotkeyModifiers);
        Assert.Equal(0x31, viewModel.ShowHotkeyVirtualKey);
        Assert.Equal("Alt + Shift + 1", viewModel.HotkeyText);
    }

    [Fact]
    public void CapturedMouseHotkeyUpdatesDisplayText()
    {
        using var viewModel = new MainWindowViewModel(new FakeVirtualDesktopService());

        viewModel.ApplyCapturedShowHotkey(new CapturedShowHotkey(
            ShowHotkeyKind.MouseButton,
            HotkeyDefinitions.DefaultHotkeyModifiers,
            HotkeyDefinitions.DefaultShowHotkeyVirtualKey,
            MouseHotkeyButton.XButton2));

        Assert.Equal(ShowHotkeyKind.MouseButton, viewModel.ShowHotkeyKind);
        Assert.Equal(MouseHotkeyButton.XButton2, viewModel.ShowHotkeyMouseButton);
        Assert.Equal("鼠标侧键 2", viewModel.HotkeyText);
    }

}
