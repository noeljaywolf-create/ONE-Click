namespace OneClick.Application.Contracts;

/// <summary>
/// Stable command identifier supplied by the caller for replay/correlation.
/// Scope and replay semantics are defined by the application boundary.
/// </summary>
public readonly record struct CommandId(string Value)
{
    public override string ToString() => Value;
}
