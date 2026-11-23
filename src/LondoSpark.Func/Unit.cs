namespace LondoSpark.Func;

/// <summary>Represents a value with no additional information.</summary>
public readonly record struct Unit
{
    /// <summary>Gets the single <see cref="Unit"/> value.</summary>
    public static Unit Value => default;
}
