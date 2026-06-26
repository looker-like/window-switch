namespace WindowSwitch.Services;

public sealed class DesktopHotkeySequence
{
    private int? _pendingFirstDigit;

    public bool IsListening => _pendingFirstDigit.HasValue;

    public int? HandleDigit(int digit, IReadOnlyCollection<int> availableDesktopIndexes)
    {
        if (digit is < 0 or > 9)
        {
            return null;
        }

        if (_pendingFirstDigit is int firstDigit)
        {
            var twoDigitIndex = firstDigit * 10 + digit;
            if (availableDesktopIndexes.Contains(twoDigitIndex))
            {
                ArmIfNeeded(digit, availableDesktopIndexes);
                return twoDigitIndex;
            }
        }

        var singleDigitIndex = digit == 0 ? 10 : digit;
        ArmIfNeeded(digit, availableDesktopIndexes);

        return availableDesktopIndexes.Contains(singleDigitIndex)
            ? singleDigitIndex
            : null;
    }

    public void Reset()
    {
        _pendingFirstDigit = null;
    }

    private void ArmIfNeeded(int digit, IReadOnlyCollection<int> availableDesktopIndexes)
    {
        if (digit == 0)
        {
            _pendingFirstDigit = null;
            return;
        }

        _pendingFirstDigit = availableDesktopIndexes.Any(index => index > 10) &&
            availableDesktopIndexes.Any(index => index >= 10 && index / 10 == digit)
            ? digit
            : null;
    }
}
