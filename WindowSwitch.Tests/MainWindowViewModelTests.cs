using WindowSwitch.Models;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;

namespace WindowSwitch.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void RefreshLoadsDesktopsAndMarksCurrent()
    {
        var currentId = Guid.NewGuid();
        var fake = new FakeVirtualDesktopService
        {
            Desktops =
            [
                new VirtualDesktopInfo(Guid.NewGuid(), 1, "Work", false),
                new VirtualDesktopInfo(currentId, 2, "Chat", true),
            ],
        };

        using var viewModel = new MainWindowViewModel(fake);

        Assert.Equal(2, viewModel.Desktops.Count);
        Assert.Equal("Work", viewModel.Desktops[0].DisplayName);
        Assert.Equal("Chat", viewModel.Desktops[1].DisplayName);
        Assert.False(viewModel.Desktops[0].IsCurrent);
        Assert.True(viewModel.Desktops[1].IsCurrent);
        Assert.False(viewModel.HasStatus);
    }

    [Fact]
    public void EmptyNameFallsBackToDesktopIndex()
    {
        var fake = new FakeVirtualDesktopService
        {
            Desktops =
            [
                new VirtualDesktopInfo(Guid.NewGuid(), 3, "   ", true),
            ],
        };

        using var viewModel = new MainWindowViewModel(fake);

        Assert.Single(viewModel.Desktops);
        Assert.Equal("Desktop 3", viewModel.Desktops[0].DisplayName);
    }

    [Fact]
    public void SwitchCommandSwitchesByIdAndRefreshes()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var fake = new FakeVirtualDesktopService
        {
            Desktops =
            [
                new VirtualDesktopInfo(first, 1, "One", true),
                new VirtualDesktopInfo(second, 2, "Two", false),
            ],
        };
        using var viewModel = new MainWindowViewModel(fake);

        fake.Desktops =
        [
            new VirtualDesktopInfo(first, 1, "One", false),
            new VirtualDesktopInfo(second, 2, "Two", true),
        ];

        viewModel.SwitchDesktopCommand.Execute(second);

        Assert.Equal(second, fake.LastSwitchedTo);
        Assert.True(viewModel.Desktops[1].IsCurrent);
    }

    [Fact]
    public void ServiceChangeEventRefreshesDesktopList()
    {
        var fake = new FakeVirtualDesktopService
        {
            Desktops =
            [
                new VirtualDesktopInfo(Guid.NewGuid(), 1, "Old", true),
            ],
        };
        using var viewModel = new MainWindowViewModel(fake);

        fake.Desktops =
        [
            new VirtualDesktopInfo(Guid.NewGuid(), 1, "New", true),
            new VirtualDesktopInfo(Guid.NewGuid(), 2, "Extra", false),
        ];
        fake.RaiseDesktopsChanged();

        Assert.Equal(2, viewModel.Desktops.Count);
        Assert.Equal("New", viewModel.Desktops[0].DisplayName);
        Assert.Equal("Extra", viewModel.Desktops[1].DisplayName);
    }

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
            isHotkeyEnabled: false,
            isDesktopHotkeysEnabled: false,
            showHotkeyModifiers: (int)(HotkeyModifiers.Control | HotkeyModifiers.Shift),
            showHotkeyVirtualKey: 0x71,
            desktopHotkeyModifiers: (int)(HotkeyModifiers.Alt | HotkeyModifiers.Shift));

        using var viewModel = new MainWindowViewModel(fake, settings);

        Assert.False(viewModel.IsFloatingTopmost);
        Assert.True(viewModel.StartHidden);
        Assert.True(viewModel.AutoHideAfterSwitch);
        Assert.False(viewModel.IsHotkeyEnabled);
        Assert.False(viewModel.IsDesktopHotkeysEnabled);
        Assert.Equal((int)(HotkeyModifiers.Control | HotkeyModifiers.Shift), viewModel.ShowHotkeyModifiers);
        Assert.Equal(0x71, viewModel.ShowHotkeyVirtualKey);
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
            desktopHotkeyModifiers: 0xFFFF);

        using var viewModel = new MainWindowViewModel(fake, settings);

        Assert.Equal(4, viewModel.ColumnsPerRow);
        Assert.Equal(0.35, viewModel.WindowOpacity);
        Assert.Equal(35, viewModel.WindowOpacityPercent);
        Assert.Equal(HotkeyDefinitions.DefaultHotkeyModifiers, viewModel.ShowHotkeyModifiers);
        Assert.Equal(HotkeyDefinitions.DefaultShowHotkeyVirtualKey, viewModel.ShowHotkeyVirtualKey);
        Assert.Equal(HotkeyDefinitions.DefaultHotkeyModifiers, viewModel.DesktopHotkeyModifiers);
    }

    [Fact]
    public void RefreshDoesNotClearExistingStatus()
    {
        var fake = new FakeVirtualDesktopService
        {
            Desktops =
            [
                new VirtualDesktopInfo(Guid.NewGuid(), 1, "One", true),
            ],
        };
        using var viewModel = new MainWindowViewModel(fake);

        viewModel.SetStatus("pin failed");
        viewModel.Refresh();

        Assert.True(viewModel.HasStatus);
        Assert.Equal("pin failed", viewModel.StatusMessage);
    }

    [Fact]
    public void SwitchCommandRaisesCompletionEvent()
    {
        var id = Guid.NewGuid();
        var fake = new FakeVirtualDesktopService
        {
            Desktops =
            [
                new VirtualDesktopInfo(id, 1, "One", true),
            ],
        };
        using var viewModel = new MainWindowViewModel(fake);
        var raised = false;
        viewModel.DesktopSwitchCompleted += (_, _) => raised = true;

        viewModel.SwitchDesktopCommand.Execute(id);

        Assert.True(raised);
    }

    private sealed class FakeVirtualDesktopService : IVirtualDesktopService
    {
        public event EventHandler? DesktopsChanged;

        public IReadOnlyList<VirtualDesktopInfo> Desktops { get; set; } = [];

        public Guid? LastSwitchedTo { get; private set; }

        public IReadOnlyList<VirtualDesktopInfo> GetDesktops()
        {
            return Desktops;
        }

        public void SwitchTo(Guid id)
        {
            LastSwitchedTo = id;
        }

        public void RaiseDesktopsChanged()
        {
            DesktopsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
