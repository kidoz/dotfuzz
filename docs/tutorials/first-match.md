# Find your first fuzzy match

[Documentation](../README.md) / Tutorials

In this tutorial, you will build a console application that compares two words
and finds a team name despite a typo.

You need a local DotFuzz checkout and the .NET SDK selected by its
[`global.json`](../../global.json): version 10.0.400 with patch roll-forward.
Run the commands from the repository root. The application will live in a sibling
directory named `DotFuzzDemo`.

## 1. Create the application

```sh
dotnet new console --framework net10.0 --output ../DotFuzzDemo
dotnet add ../DotFuzzDemo/DotFuzzDemo.csproj reference src/DotFuzz/DotFuzz.csproj
```

The second command connects the application to the library in your checkout.

## 2. Compare two words

Replace `../DotFuzzDemo/Program.cs` with:

```csharp
using DotFuzz;

Console.WriteLine(Fuzz.Ratio("fuzzy", "wuzzy"));
```

Run it:

```sh
dotnet run --project ../DotFuzzDemo/DotFuzzDemo.csproj -c Release
```

The application prints:

```text
80
```

A score of 100 means identical non-empty text. The changed first letter lowers
this pair's score to 80.

## 3. Find a name despite a typo

Replace `Program.cs` with this complete program:

```csharp
using DotFuzz;
using DotFuzz.Cached;

string[] choices = ["Atlanta Falcons", "New York Jets", "Dallas Cowboys"];
string query = Preprocess.Compatibility("NEW YORK JTS!");
string[] normalized = Array.ConvertAll(choices, Preprocess.Compatibility);

var scorer = new CachedRatio(query);
ExtractOneResult best = Process.ExtractOne(scorer, normalized, scoreCutoff: 80);

if (best.Found)
{
    Console.WriteLine($"{choices[best.Index]}: {best.Score}");
}
else
{
    Console.WriteLine("No match");
}
```

Run the same `dotnet run` command again. The application prints:

```text
New York Jets: 96
```

You explicitly normalized both the query and choices before comparing them.
`best.Index` identifies the original choice so you can display its original
capitalization. `best.Found` tells you whether a candidate reached the cutoff.

## 4. Require an exact match

Change `scoreCutoff: 80` to `scoreCutoff: 100` and run the application again.
The output becomes:

```text
No match
```

The typo prevents a score of 100, so no candidate qualifies.

You have compared a pair, normalized a collection, and found a candidate using
a reusable query scorer. Continue with [Match and rank choices](../how-to/match-and-rank.md)
for top-k results and caller-owned buffers. Consult the
[API reference](../reference/api.md) for method contracts.
