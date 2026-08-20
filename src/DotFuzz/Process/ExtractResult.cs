namespace DotFuzz;

/// <summary>A qualifying choice returned by <see cref="Process"/> extraction.</summary>
public readonly record struct ExtractResult(string Value, int Score, int Index);
