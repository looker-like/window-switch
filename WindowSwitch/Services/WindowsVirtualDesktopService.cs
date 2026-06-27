using WindowSwitch.Models;
using WindowsDesktop;
using System.Runtime.InteropServices;

namespace WindowSwitch.Services;

public sealed class WindowsVirtualDesktopService : IVirtualDesktopService
{
    private const ushort VkTab = 0x09;
    private const ushort VkLeft = 0x25;
    private const ushort VkRight = 0x27;
    private const ushort VkD = 0x44;
    private const ushort VkF4 = 0x73;
    private const ushort VkLeftWindows = 0x5B;
    private const ushort VkControl = 0x11;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private static readonly int NativeInputSize = Marshal.SizeOf<NativeInput>();

    public event EventHandler? DesktopsChanged;

    public WindowsVirtualDesktopService()
    {
        TrySubscribeToDesktopEvents();
    }

    public IReadOnlyList<VirtualDesktopInfo> GetDesktops()
    {
        EnsureSupported();

        var current = VirtualDesktop.Current;
        return VirtualDesktop.GetDesktops()
            .Select((desktop, index) => new VirtualDesktopInfo(
                desktop.Id,
                index + 1,
                desktop.Name ?? string.Empty,
                current is not null && desktop.Id == current.Id))
            .ToArray();
    }

    public void SwitchTo(Guid id)
    {
        EnsureSupported();

        var desktop = VirtualDesktop.FromId(id);
        if (desktop is null)
        {
            return;
        }

        desktop.Switch();
    }

    public void ExecuteAction(VirtualDesktopAction action)
    {
        switch (action)
        {
            case VirtualDesktopAction.OpenTaskView:
                SendShortcut(VkLeftWindows, VkTab);
                break;
            case VirtualDesktopAction.CreateDesktop:
                SendShortcut(VkLeftWindows, VkControl, VkD);
                break;
            case VirtualDesktopAction.SwitchRight:
                SendShortcut(VkLeftWindows, VkControl, VkRight);
                break;
            case VirtualDesktopAction.SwitchLeft:
                SendShortcut(VkLeftWindows, VkControl, VkLeft);
                break;
            case VirtualDesktopAction.CloseCurrentDesktop:
                SendShortcut(VkLeftWindows, VkControl, VkF4);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    private static void EnsureSupported()
    {
        if (!VirtualDesktop.IsSupported)
        {
            throw new PlatformNotSupportedException("Virtual desktop API is not supported on this Windows build.");
        }
    }

    private void TrySubscribeToDesktopEvents()
    {
        try
        {
            VirtualDesktop.Created += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.Destroyed += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.CurrentChanged += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.Switched += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.Moved += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.Renamed += (_, _) => RaiseDesktopsChanged();
        }
        catch
        {
            // GetDesktops surfaces the compatibility error in the UI; startup should stay alive.
        }
    }

    private void RaiseDesktopsChanged()
    {
        DesktopsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void SendShortcut(params ushort[] virtualKeys)
    {
        var inputs = new NativeInput[virtualKeys.Length * 2];
        var index = 0;

        foreach (var virtualKey in virtualKeys)
        {
            inputs[index++] = CreateKeyboardInput(virtualKey, 0);
        }

        for (var i = virtualKeys.Length - 1; i >= 0; i--)
        {
            inputs[index++] = CreateKeyboardInput(virtualKeys[i], KeyEventKeyUp);
        }

        var sent = SendInput((uint)inputs.Length, inputs, NativeInputSize);
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException($"无法发送 Windows 虚拟桌面快捷键。Win32 错误码：{Marshal.GetLastPInvokeError()}。");
        }
    }

    private static NativeInput CreateKeyboardInput(ushort virtualKey, uint flags)
    {
        return new NativeInput
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = flags,
                },
            },
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParamLow;
        public ushort ParamHigh;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, NativeInput[] inputs, int inputSize);
}
