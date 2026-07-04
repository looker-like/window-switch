using WindowSwitch.Models;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;

namespace WindowSwitch.Tests;

public sealed partial class MainWindowViewModelTests
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

}
