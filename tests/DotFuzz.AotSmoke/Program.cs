using DotFuzz;
using DotFuzz.Cached;
using DotFuzz.Distance;

string[] choices = ["alpha", "alpine", "omega"];
var cached = new CachedRatio("alpah");
var result = Process.ExtractOne(cached, choices, 40);
var top = Process.ExtractTop(cached, choices, 2, 10);
var all = Process.ExtractAll(cached, choices, 10);

Span<char> processed = stackalloc char[32];
var processedLength = Preprocess.Compatibility("New York Mets!".AsSpan(), processed);
var unicodeLength = Preprocess.Unicode("Тёплый Stanley — 42".AsSpan(), processed);

return
    Indel.Distance("kitten", "sitting") == 5
    && Fuzz.Ratio("fuzzy", "wuzzy") == 80
    && Fuzz.PartialRatio("similar", "somewhat similar") == 100
    && Fuzz.TokenSortRatio("fuzzy wuzzy was a bear", "wuzzy fuzzy was a bear") == 100
    && Fuzz.TokenSetRatio("fuzzy was a bear", "fuzzy fuzzy was a bear") == 100
    && Fuzz.PartialTokenSortRatio("bear a", "a bear fuzzy") == 100
    && Fuzz.PartialTokenSetRatio("a bear", "fuzzy was a bear") == 100
    && Fuzz.WeightedRatio("this is a test", "this is a test!") == 97
    && processedLength == 13
    && unicodeLength > 0
    && result is { Found: true, Value: "alpha" }
    && top.Length == 2
    && all.Length == 3
    ? 0
    : 1;
