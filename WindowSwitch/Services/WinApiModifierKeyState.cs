using System.Runtime.InteropServices;

namespace WindowSwitch.Services;

/// <summary>
/// 生产环境下的修饰键状态实现，调用 user32.dll 的 GetAsyncKeyState。
/// </summary>
public sealed class WinApiModifierKeyState : IModifierKeyState
{
    public static readonly WinApiModifierKeyState Instance = new();

    private WinApiModifierKeyState() { }

    public bool IsPressed(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
