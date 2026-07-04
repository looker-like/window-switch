using WindowSwitch.Models;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;

namespace WindowSwitch.Tests;

public sealed partial class MainWindowViewModelTests
{
    [Fact]
    public void GestureSelectionMarksOnlySelectedDesktop()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var fake = new FakeVirtualDesktopService
        {
            Desktops =
            [
                new VirtualDesktopInfo(first, 1, "One", false),
                new VirtualDesktopInfo(second, 2, "Two", true),
            ],
        };
        using var viewModel = new MainWindowViewModel(fake);

        viewModel.SetGestureSelectedDesktop(first);

        Assert.True(viewModel.Desktops[0].IsGestureSelected);
        Assert.False(viewModel.Desktops[1].IsGestureSelected);

        viewModel.ClearGestureSelection();

        Assert.False(viewModel.Desktops[0].IsGestureSelected);
        Assert.False(viewModel.Desktops[1].IsGestureSelected);
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

    [Fact]
    public void VirtualDesktopActionsExposeRequestedShortcuts()
    {
        using var viewModel = new MainWindowViewModel(new FakeVirtualDesktopService());

        Assert.Collection(
            viewModel.VirtualDesktopActions,
            action =>
            {
                Assert.Equal(VirtualDesktopAction.OpenTaskView, action.Action);
                Assert.Equal("打开任务视图", action.DisplayName);
                Assert.Equal("Win + Tab", action.ShortcutText);
            },
            action =>
            {
                Assert.Equal(VirtualDesktopAction.CreateDesktop, action.Action);
                Assert.Equal("新建虚拟桌面", action.DisplayName);
                Assert.Equal("Win + Ctrl + D", action.ShortcutText);
            },
            action =>
            {
                Assert.Equal(VirtualDesktopAction.SwitchLeft, action.Action);
                Assert.Equal("切到左侧桌面", action.DisplayName);
                Assert.Equal("Win + Ctrl + ←", action.ShortcutText);
            },
            action =>
            {
                Assert.Equal(VirtualDesktopAction.SwitchRight, action.Action);
                Assert.Equal("切到右侧桌面", action.DisplayName);
                Assert.Equal("Win + Ctrl + →", action.ShortcutText);
            },
            action =>
            {
                Assert.Equal(VirtualDesktopAction.CloseCurrentDesktop, action.Action);
                Assert.Equal("关闭当前虚拟桌面", action.DisplayName);
                Assert.Equal("Win + Ctrl + F4", action.ShortcutText);
            });
    }

    [Fact]
    public void VirtualDesktopActionCommandExecutesServiceActionAndRaisesCompletionEvent()
    {
        var fake = new FakeVirtualDesktopService
        {
            Desktops =
            [
                new VirtualDesktopInfo(Guid.NewGuid(), 1, "One", true),
            ],
        };
        using var viewModel = new MainWindowViewModel(fake);
        var raised = false;
        viewModel.DesktopSwitchCompleted += (_, _) => raised = true;

        viewModel.ExecuteVirtualDesktopActionCommand.Execute(VirtualDesktopAction.CreateDesktop);

        Assert.Equal(VirtualDesktopAction.CreateDesktop, fake.LastAction);
        Assert.True(raised);
        Assert.Single(viewModel.Desktops);
    }

    [Fact]
    public void GestureSelectionMarksOnlySelectedVirtualDesktopAction()
    {
        using var viewModel = new MainWindowViewModel(new FakeVirtualDesktopService());

        viewModel.SetGestureSelectedVirtualDesktopAction(VirtualDesktopAction.SwitchRight);

        Assert.True(viewModel.VirtualDesktopActions.Single(action => action.Action == VirtualDesktopAction.SwitchRight).IsGestureSelected);
        Assert.All(
            viewModel.VirtualDesktopActions.Where(action => action.Action != VirtualDesktopAction.SwitchRight),
            action => Assert.False(action.IsGestureSelected));

        viewModel.ClearGestureSelection();

        Assert.All(viewModel.VirtualDesktopActions, action => Assert.False(action.IsGestureSelected));
    }

}
