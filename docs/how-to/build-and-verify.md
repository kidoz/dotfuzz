# Build and verify

[Documentation](../README.md) / How-to guides

Run these commands from the repository root with the SDK selected by
[`global.json`](../../global.json). Its `latestPatch` policy selects patches in
the 11.0.100 feature band. Install the .NET 10 runtime to run the tests on both
frameworks. Package restore requires access to the configured NuGet source or
an already populated cache.

## Restore, format-check, build, and test

```sh
dotnet tool restore
dotnet restore DotFuzz.slnx --locked-mode
dotnet csharpier check src
dotnet csharpier check tests
dotnet csharpier check benchmarks
dotnet build DotFuzz.slnx -c Release --no-restore
dotnet test DotFuzz.slnx -c Release --no-build --no-restore
```

These are the checks in the [build workflow](../../.github/workflows/build.yml).
Formatting checks leave source files unchanged. To fix reported formatting, run
`dotnet csharpier format src tests benchmarks`, then repeat the check.

If locked restore fails because package declarations and lock files differ,
resolve that difference before proceeding. The SDK uses Microsoft.Testing.Platform
as configured in `global.json`.

## Publish and run the NativeAOT smoke application

Use a host with the native build prerequisites required by .NET NativeAOT.
The [NativeAOT workflow](../../.github/workflows/aot.yml) runs this on Ubuntu
and macOS for both frameworks. In a POSIX shell, select `net10.0` or `net11.0`
and use the host runtime identifier.

```sh
framework=net11.0
rid=$(dotnet --info | awk '/RID:/ { print $2 }')
dotnet publish tests/DotFuzz.AotSmoke/DotFuzz.AotSmoke.csproj \
  -c Release -f "$framework" -r "$rid" --self-contained
"tests/DotFuzz.AotSmoke/bin/Release/$framework/$rid/publish/DotFuzz.AotSmoke"
```

The executable is silent on success and exits with code 0. A nonzero exit code
means a smoke assertion failed. The project already enables `PublishAot`; a
separate publish property is unnecessary.

See [Architecture](../explanation/architecture.md#nativeaot-posture) for the library's
NativeAOT design and [Run benchmarks](run-benchmarks.md) for performance checks.
