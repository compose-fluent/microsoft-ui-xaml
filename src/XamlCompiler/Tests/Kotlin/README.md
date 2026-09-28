# Kotlin declaration protocol

The existing executable accepts `Language: "Kotlin"`, `LanguageSourceExtension: ".kt"`, and `IsPass1: true` in its normal input JSON. Supply the same WinMD reference set as the application, reference **directories** in `ReferenceAssemblyPaths`, and a distinct output/saved-state directory. The output JSON's `KotlinDeclarations` property is a complete schema-version-1 snapshot. Error output must not be consumed as a declaration input.

The snapshot contains pages sorted by `x:Class`, relative resource paths, WinRT base and element names, harvester connection IDs, fields, event/delegate names and 1-based source locations. Classless resources have their own list. It deliberately does not guess Kotlin projection names. Kotlin resolves these using its metadata/generator mapping. Empty input produces an empty snapshot. Kotlin analysis always visits every input; the build caller owns incremental checks.

Currently this mode supports known SDK element types, named elements and ordinary events. Application-local element types, bindings, template scopes and deferred loading are rejected until their semantic/type-input slices are implemented. Final Kotlin compilation is explicitly unavailable at this stage; successful declaration analysis does not imply XBF generation or runtime support.

Run `Test-DeclarationIndex.ps1` with `-Compiler`, `-ReferenceDirectories` and `-OutputDirectory`. It invokes the real executable and validates repeat/rename/deletion/resource handling, unsupported-feature failure and existing C#/CppWinRT Pass 1 source generation. Output files are retained for inspection.

An optional `KotlinSymbols` input carries the schema-version-1 semantic sidecar from Kotlin compilation: the declaration snapshot and its fingerprint, plus each page's ordinary handler names, return types and parameter types. When present, declarations must match the current harvest and handler signatures must match the actual WinMD delegate `Invoke` method. Private methods are represented only in this compile-time sidecar. The smoke script also checks valid/invalid signatures and stale declarations.

The Kotlin consumer fixture was generated with Windows App SDK 2.5.1's WinUI 2.3.9, Foundation 2.3.12, InteractiveExperiences 2.1.9 (metadata/10.0.18362.0), and Windows SDK contracts 10.0.26100.1742. These are test provenance, not hard-coded protocol dependencies.
