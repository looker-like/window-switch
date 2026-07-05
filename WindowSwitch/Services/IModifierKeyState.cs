namespace WindowSwitch.Services;

/// <summary>
/// 抽象修饰键（Ctrl/Alt/Shift/Win）的物理状态查询，便于在测试中替换为内存实现。
/// </summary>
public interface IModifierKeyState
{
    bool IsPressed(int virtualKey);
}
