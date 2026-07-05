using WindowSwitch.Services;

namespace WindowSwitch.Tests;

/// <summary>
/// 内存驱动的修饰键状态实现，仅用于单元测试。
/// </summary>
internal sealed class FakeModifierKeyState : IModifierKeyState
{
    private readonly HashSet<int> _pressedKeys;

    public FakeModifierKeyState(params int[] pressedVirtualKeys)
    {
        _pressedKeys = [..pressedVirtualKeys];
    }

    public bool IsPressed(int virtualKey) => _pressedKeys.Contains(virtualKey);
}
