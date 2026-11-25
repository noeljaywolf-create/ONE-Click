namespace OneClick.Application.Contracts;

/// <summary>
/// Strong project revision/precondition token used for optimistic concurrency.
/// Commands that mutate accepted state must be evaluated against the exact expected revision.
/// </summary>
public readonly record struct ProjectRevision(long Value)
{
    public static ProjectRevision Initial => new(0);

    public override string ToString() => Value.ToString();
}
