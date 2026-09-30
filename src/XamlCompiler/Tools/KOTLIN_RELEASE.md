# Kotlin XamlCompiler packages

The `kotlin-xamlc` branch owns the Kotlin backend. The `Kotlin XamlCompiler`
workflow builds the executable and both shared compiler targets, runs the Kotlin
declaration/diagnostic and existing C#/CppWinRT smoke suite, and uploads a package.
A `kotlin-xamlc-v<version>` tag publishes that package as a GitHub release in this
repository. Branch builds produce CI artifacts only.

`Package-KotlinXamlCompiler.ps1` accepts the built executable directory, an exact
version, and an output directory. It produces:

- `kotlin-xamlc-<version>-win-x64.zip`
- The archive's `.sha256` file.
- Inside the archive, `kotlin-xamlc.json` identifies package schema 1, Kotlin
  protocol 2, host `win-x64`, source and upstream revisions, entry point, and every payload file's
  SHA-256. The Kotlin plugin pins the archive digest as well as checking this manifest.

The host needs .NET Framework 4.7.2 or later. GenXbf is obtained from the
application's resolved Windows App SDK WinUI NuGet package; it is not redistributed
in this compiler package. GenXbf's architecture must match the compiler process,
not the architecture of the application being compiled.

The manifest records executable invocation, the .NET Framework prerequisite,
and the verified Windows App SDK 2.5.1 / WinUI package 2.3.9 combination. CI
unpacks the archive into an isolated directory, runs the existing regression
suite using that entry point, and compiles a resource dictionary to actual XBF
before uploading or publishing the package.

When updating the Kotlin plugin's release pin, copy the version and archive digest
from the completed CI release, then validate Gallery without a local compiler
override. Never use a moving `latest` URL, an unverified download, or the stock SDK
XamlCompiler as a fallback. A local checkout override is for development and must
still produce compatible protocol output.
