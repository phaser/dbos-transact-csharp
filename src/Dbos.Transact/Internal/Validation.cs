namespace Dbos.Transact.Internal;

internal static class Validation
{
    public static bool NullableIsEmpty(string? value) => value != null && value.Length == 0;

    public static bool NullableIsNotPositive(TimeSpan? value) =>
        value.HasValue && value.Value <= TimeSpan.Zero;
}
