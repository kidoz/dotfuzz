# Match and rank choices

[Documentation](../README.md) / How-to guides

Use these recipes in an application that references DotFuzz. If you need to set
one up, follow [Find your first fuzzy match](../tutorials/first-match.md).

## Normalize and find the best choice

Apply the same preprocessor to the query and every candidate. Use
`Preprocess.Compatibility` for ASCII-oriented FuzzySharp full preprocessing, or
`Preprocess.Unicode` to retain Unicode letters and digits.

```csharp
using DotFuzz;
using DotFuzz.Cached;

string[] choices = ["Atlanta Falcons", "New York Jets", "Dallas Cowboys"];
string[] normalized = Array.ConvertAll(choices, Preprocess.Compatibility);
var scorer = new CachedRatio(Preprocess.Compatibility("NEW YORK JTS!"));

ExtractOneResult best = Process.ExtractOne(scorer, normalized, scoreCutoff: 80);
if (best.Found)
{
    Console.WriteLine(choices[best.Index]); // New York Jets
}
```

Keep the normalized array while the candidate set is unchanged. Reuse the scorer
for repeated scans with the same normalized query. Choose your cutoff using
representative matches and non-matches from your application.

`Value` contains the normalized candidate in this example. Use `Index` to retrieve
the original value. Always check `Found` before indexing for an `ExtractOne` result.

## Return ranked matches or all qualifying choices

With `scorer` and `normalized` from the example above:

```csharp
ExtractResult[] top = Process.ExtractTop(scorer, normalized, limit: 2, scoreCutoff: 50);
ExtractResult[] all = Process.ExtractAll(scorer, normalized, scoreCutoff: 50);
```

Use `ExtractTop` for descending scores, with earlier candidate indexes winning
ties. Use `ExtractAll` when you need every qualifying choice in input order.

## Reuse a result buffer

Allocate a buffer once and pass it to subsequent scans. Use the returned count to
read only the written entries:

```csharp
ExtractResult[] buffer = new ExtractResult[2];
int count = Process.ExtractTop(scorer, normalized, buffer.AsSpan(), scoreCutoff: 50);
foreach (ExtractResult match in buffer.AsSpan(0, count))
{
    Console.WriteLine($"{choices[match.Index]}: {match.Score}");
}
```

For `ExtractAll`, allocate at least `normalized.Length` entries. `ExtractResult`
contains a string reference, so use an array rather than `stackalloc`. See the
[API reference](../reference/api.md#extraction) for buffer and allocation contracts.

## Score reordered words or embedded phrases

Call a direct scorer when your task requires a different scoring rule:

```csharp
int reordered = Fuzz.TokenSortRatio("new york jets", "jets new york"); // 100
int embedded = Fuzz.PartialRatio("yankees", "new york yankees"); // 100
int repeated = Fuzz.TokenSetRatio("fuzzy bear", "fuzzy fuzzy bear"); // 100
```

Normalize these inputs first if your task needs case or punctuation normalization.
`Process` extraction always uses direct `Ratio`. To rank with another scorer,
score your candidates with that method and order the results in your application.
See [Compatibility](../reference/compatibility.md#api-differences) when migrating
FuzzySharp extraction code.
