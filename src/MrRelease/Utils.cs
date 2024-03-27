namespace MrRelease;

public static class ProgressStrings
{
    public const string Reset = "\x1b]9;4;0;0;\x07";
    public static string Default(int progress)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(progress);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(progress, 100);

        return $"\x1b]9;4;1;{progress};\x07";
    }

    public static string Error(int progress)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(progress);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(progress, 100);

        return $"\x1b]9;4;2;{progress};\x07";
    }

    public const string Indeterminate = "\x1b]9;4;3;0;\x07";

    public static string Warning(int progress)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(progress);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(progress, 100);

        return $"\x1b]9;4;4;{progress};\x07";
    }
}
