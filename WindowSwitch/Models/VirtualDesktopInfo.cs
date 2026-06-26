namespace WindowSwitch.Models;

public sealed record VirtualDesktopInfo(Guid Id, int Index, string Name, bool IsCurrent);
