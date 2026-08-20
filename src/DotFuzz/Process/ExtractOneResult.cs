namespace DotFuzz;

/// <summary>The best choice returned by <see cref="Process"/> extraction.</summary>
public readonly record struct ExtractOneResult(string? Value, int Score, int Index)
{
    /// <summary>Gets whether a choice met the requested cutoff.</summary>
    public bool Found => Index >= 0;

    /// <summary>Represents no qualifying choice.</summary>
    public static ExtractOneResult None => new(null, 0, -1);
}
