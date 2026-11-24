namespace LondoSpark.Func;

/// <summary>Names the input that failed validation and why, so failures can be mapped to field-keyed responses.</summary>
/// <param name="Field">The input that failed, for example <c>email</c>.</param>
/// <param name="Message">The human-readable reason.</param>
public readonly record struct ValidationError(string Field, string Message)
{
    /// <summary>Renders the error as <c>field: message</c>.</summary>
    /// <returns>The field and message separated by a colon.</returns>
    public override string ToString() =>
        $"{Field}: {Message}";
}
