# Pulsar Build and Process Architecture Plan

## Goals

- Make `dotnet build` work on Windows and Linux.
- Keep ordinary builds side-effect-free.
- Place every project's output under one predictable artifacts root without
  allowing projects or target frameworks to overwrite each other.
- Support full solution builds and direct, partial project builds.
- Replace Windows batch deployment with cross-platform MSBuild targets.
- Produce separate Windows/Proton and native Linux release archives.
- Preserve one external updater executable for Windows and Proton while using
  in-process updates on native Linux.
- Move plugin compilation out of the launcher process.
- Add one small `Pulsar.Protocol` project for compiler and independent UI wire
  contracts.
- Leave clean integration points for protected GitHub Actions builds and code
  signing.

## Non-goals

- Do not redistribute Space Engineers client assemblies.
- Do not parse every possible Steam installation layout inside MSBuild.
- Do not make the compiler process a security sandbox. Compiled plugins still
  load into Pulsar with full trust.
- Do not support multiple protocol versions in one running host. Components in
  a release are shipped together.
- Do not add a service, daemon, socket server, dependency injection framework,
  or general-purpose message bus.
- Do not add strong-name signing unless an actual assembly identity requirement
  appears.

## Decisions

| Area | Decision |
| --- | --- |
| Normal build | Write isolated artifacts only |
| Deployment | Explicit `Deploy` target |
| Full package layout | Explicit `Stage` target |
| Partial deploy | Preserve unrelated launcher outputs |
| Release archives | Separate Windows/Proton and native Linux ZIP files |
| Proton | Use the Windows build and Windows compiler worker |
| Native Linux | Use Interim/Modern and a native net10 compiler worker |
| Windows compiler | net48, so Legacy systems do not require net10 |
| Linux compiler | net10.0, using the runtime required by the native launcher |
| External updater | One net48 Windows executable for Windows and Proton |
| Native Linux update | Apply in-process, then restart |
| Protocol assemblies | One `Pulsar.Protocol` assembly |
| CI game references | Defer provisioning; allow protected self-hosted runners |
| Windows signing | Authenticode after staging and before packaging |

## Current State

### Project graph

The current effective project graph is:

```text
Compiler/netstandard2.0
        ^
Shared/net48 ---------------- Legacy/net48       -> Legacy.exe
Shared/net10.0-windows ------ Legacy/net10       -> Interim
Shared/net10.0-windows ------ Modern/net10       -> Modern
Shared/net8.0-windows                                unused

Updater/net48 -----------------------------------> Updater.exe
```

All projects are solution roots. `Shared` references `Compiler`, so builds that
do not compile plugins themselves still inherit Roslyn and Cecil compiler
dependencies.

### Build problems

- `Directory.Build.props` uses MSBuild registry properties. They are not
  supported by the .NET SDK form of MSBuild used by `dotnet build`.
- `Legacy`, `Modern`, and `Updater` execute `.bat` files during every build.
- The scripts deploy directly into a live Pulsar installation.
- The scripts depend on `$(SolutionDir)`, which is unreliable when a project is
  built directly.
- `verify.bat` reports missing paths but always exits successfully.
- Configuration property groups depend on `Configuration|Platform`. A direct
  project build defaults to `AnyCPU`, so its behavior differs from a solution
  build even though `PlatformTarget` is x64.
- Manual dependency copy lists are incomplete and can silently omit transitive,
  native, and satellite runtime assets.
- Parallel solution builds can deploy partially updated output into the same
  destination.
- `Shared` always resolves `Steamworks.NET` through SE1, so a Modern-only build
  unnecessarily requires the SE1 installation.
- `Shared/net8.0-windows` has no consumer in the repository.
- There is no SDK pin, package staging project, workflow, signing boundary, or
  documented build command.

### Compiler problems

The compiler API is currently stateful:

```text
ICompilerFactory.Init()
ICompilerFactory.Create(debugBuild)
ICompiler.Load(source stream, name, embedded path)
ICompiler.TryAddDependency(path)
ICompiler.Compile(name, out symbols)
```

Legacy isolates the compiler with an AppDomain. Interim and Modern use a
collectible `AssemblyLoadContext` plus reflection wrappers. These mechanisms
exist primarily to keep Roslyn and compiler-only dependencies away from game
assemblies. They can be deleted once compilation is in another process.

Plugin downloads, NuGet package selection, cache management, and final assembly
loading already happen in the host. They should remain there.

## Target Project Graph

```text
Pulsar.Protocol/netstandard2.0
        ^                    ^
        |                    |
Compiler/(net48|net10.0)     Shared/(net48|net10.0-windows)
                                     ^              ^
                                     |              |
                           Legacy              Modern
                         (Windows: net48 +      net10.0-windows
                          net10.0-windows,
                          Linux: net10.0-windows)

Updater/net48
Pulsar.UI/(runtime decision pending) -> Pulsar.Protocol
```

`Legacy` and `Modern` also have a build-only project reference to `Compiler` so
the platform worker is produced when either launcher is built. That reference
must use `ReferenceOutputAssembly="false"`; the launcher must not load the
worker assembly.

Remove `Shared/net8.0-windows` unless an external consumer is identified before
implementation.

## Repository Build Configuration

### `global.json`

Pin a .NET 10 SDK feature band and use a controlled roll-forward policy. This
makes local and CI output predictable while allowing approved servicing SDKs.

### `Directory.Build.props`

Keep shared build policy committed. It should define:

```text
RepoRoot
ArtifactsPath
UseArtifactsOutput=true
PlatformTarget=x64
EnableWindowsTargeting=true on non-Windows
ContinuousIntegrationBuild=true under GitHub Actions
```

Remove project-specific `OutputPath` values. The SDK artifacts layout provides:

```text
artifacts/bin/<project>/<configuration_tfm_rid>/
artifacts/obj/<project>/<configuration_tfm_rid>/
artifacts/publish/<project>/<configuration_tfm_rid>/
```

Configuration-specific settings that are genuinely required should depend only
on `$(Configuration)`, not `$(Configuration)|$(Platform)`. Prefer SDK defaults
where they already provide the desired behavior.

Add `Microsoft.NETFramework.ReferenceAssemblies.net48` privately for net48
inner builds so the projects can be compiled with `dotnet build` when the
machine does not provide reference assemblies through Visual Studio.

### Local paths

Import an optional ignored file:

```text
Directory.Build.local.props
```

Add a committed example showing `Bin64`, `Game2`, `Steamworks`, and `Pulsar`.
Command-line global properties and environment properties remain the highest
priority.

Path resolution order is:

1. Command-line or environment property.
2. `Directory.Build.local.props`.
3. Standard platform-specific Steam location.
4. A targeted build error explaining the missing property and required files.

Windows fallbacks should check the standard Steam directories without using
MSBuild registry properties.

Linux fallbacks should check:

```text
$STEAM_DIR
$STEAM_HOME
~/.steam/steam
~/.local/share/Steam
~/.var/app/com.valvesoftware.Steam/.local/share/Steam
```

Check the SE1 client app `244850` and SE2 client app `1133870`. Do not include
the dedicated server paths from the referenced server plugin template.

MSBuild should not contain a VDF parser. Custom Steam libraries use the local
props override. If this becomes a recurring support problem, add one standalone
configuration command that parses `libraryfolders.vdf` and the app manifests,
then writes `Directory.Build.local.props`. Do not put that parser on every build
path.

Set `Steamworks` from the first valid explicit or detected file:

```text
$(Bin64)/Steamworks.NET.dll
$(Game2)/Steamworks.NET.dll
```

This lets Modern build without an SE1 installation.

### `Directory.Build.targets`

Replace `verify.bat` with project-scoped validation using MSBuild `Error` tasks.
Validation should run only for projects that need a path:

| Project | Required input |
| --- | --- |
| Protocol | None |
| Compiler | None at build time |
| Updater | None |
| Shared | A valid `Steamworks.NET.dll` |
| Legacy | Valid `Bin64` files |
| Modern | Valid `Game2` files |

Validate representative required files rather than only checking directories.
Compiler, Protocol, and Updater partial builds must not require a game install.

## Build, Deploy, Stage, and Pack

### Build

Ordinary builds never write to a Pulsar installation:

```shell
dotnet build Pulsar.sln -c Release
dotnet build Legacy/Legacy.csproj -c Debug -f net10.0-windows
dotnet build Modern/Modern.csproj -c Release
dotnet build Updater/Updater.csproj -c Release
```

Building the solution produces the same isolated project and framework outputs
as building each project separately.

### Partial deploy

Add a `Deploy` target to launcher projects. It depends on `Build` and updates
only files owned by that project and target framework.

Examples:

```shell
dotnet msbuild Legacy/Legacy.csproj -restore -t:Deploy \
  -p:TargetFramework=net48 -p:Pulsar=/path/to/install

dotnet msbuild Modern/Modern.csproj -restore -t:Deploy \
  -p:Pulsar=/path/to/install
```

Ownership rules:

| Producer | Owned destination |
| --- | --- |
| Legacy/net48 | Root Legacy files and `Libraries/Legacy` |
| Legacy/net10 | Root Interim files and `Libraries/Interim` |
| Modern | Root Modern files and `Libraries/Modern` |
| Compiler | Current protocol-versioned compiler directory |
| Updater | Root updater files on Windows/Proton |

A partial deploy may recreate its own library subtree. It must not clear the
root installation, another launcher's subtree, or another compiler protocol
version.

Use resolved copy-local MSBuild items, including destination subpaths, for
runtime dependencies. Do not maintain lists of selected NuGet DLL names. Game
references remain `Private="false"` and therefore are not deployed.

Root launcher sidecars are explicit:

```text
apphost executable
managed entry DLL where applicable
runtimeconfig.json where applicable
deps.json where applicable
exe.config where applicable
LICENSE
```

Every required copy must fail the target on error.

### Full staging

Add a top-level `Pulsar.proj` with a `Stage` target. It should:

1. Select Windows or Linux from the current build OS.
2. Recreate only its platform staging directory.
3. Build the required projects.
4. Invoke project deploy targets sequentially into the staging directory.
5. Validate the final package manifest.

Expected roots:

```text
artifacts/stage/windows/Release/Pulsar/
artifacts/stage/linux/Release/Pulsar/
```

The Windows stage contains Legacy, Interim, Modern, the net48 compiler worker,
and `Updater.exe`. It is also the Proton package.

The Linux stage contains Interim, Modern, and the net10 compiler worker. It does
not contain the external Windows updater.

### Packing

`Pulsar.proj` should expose a `Pack` target that only packages an existing,
validated stage. It must not silently rebuild or restage after signing.

Outputs:

```text
Pulsar-Windows-v<version>.zip
Pulsar-Linux-v<version>.zip
Updater-v<version>.exe
```

Keep `Stage` and `Pack` separate so CI can sign the Windows stage between them.

Delete `verify.bat` and all project `deploy.bat` files after the MSBuild targets
are verified. Do not add equivalent shell scripts.

## `Pulsar.Protocol`

Create one project targeting `netstandard2.0` so it is consumable by net48 and
net10 projects.

Suggested layout:

```text
Protocol/
  Pulsar.Protocol.csproj
  Transport/
    FrameReader.cs
    FrameWriter.cs
  Compiler/
    CompilerProtocolVersion.cs
    InitializeRequest.cs
    InitializeResponse.cs
    CompileRequest.cs
    CompileResponse.cs
    SourceInput.cs
    CompilerDiagnostic.cs
    CompilerError.cs
  UI/
    UiProtocolVersion.cs
    UiHello.cs
    UiStateSnapshot.cs
    UiCommand.cs
    UiEvent.cs
    UiError.cs
```

Only add UI messages as screens are actually moved. The names above define the
intended boundaries, not a requirement to create unused DTOs immediately.

Protocol rules:

- Use separate compiler and UI protocol version constants.
- Do not derive compatibility from the Protocol assembly version.
- Keep DTOs as simple classes and primitive collections.
- Do not expose Roslyn, Cecil, WinForms, Avalonia, game, plugin, cache, NuGet, or
  `Assembly` types.
- Do not serialize exceptions.
- Do not put host interfaces or application services in Protocol.
- Keep transport framing small and synchronous until concurrency is needed.

Both current process boundaries can use four-byte length-prefixed JSON frames
over redirected stdin/stdout. stdout is reserved for protocol frames; logs go
to stderr. Reusing framing does not require a generic message bus or shared
process lifecycle abstraction.

Splitting Protocol becomes worthwhile only if compiler and UI contracts are
distributed, versioned, or consumed independently enough that carrying the
other contract is a measurable problem.

## Compiler Process

### Boundary

The launcher remains responsible for:

- Downloading GitHub source archives.
- Enumerating local source files.
- Selecting and downloading NuGet packages.
- Maintaining plugin caches and manifests.
- Selecting target symbols and reference names.
- Loading the resulting PE and PDB bytes.
- Resolving plugin runtime dependencies.

The compiler worker is responsible for:

- Building Roslyn syntax trees.
- Resolving the supplied target reference graph.
- Applying `IgnoresAccessChecksTo` publicization.
- Compiling PE and optional portable PDB output.
- Returning structured diagnostics.

### Host facade

Move `ICompiler` and `ICompilerFactory` into Shared, outside Protocol. Preserve
their current shape initially so `GitHubPlugin` and `LocalFolderPlugin` need
minimal changes.

The process-backed `ICompiler` accumulates source bytes and custom reference
paths in the host. `Compile` sends one complete request and maps diagnostic
responses back to the existing `AggregateException` behavior. This keeps the
first migration focused on isolation rather than changing plugin error paths.

### Messages

An initialization request contains:

```text
CompilerProtocolVersion
ReferenceNames
ProbeDirectories
TargetRuntime
PreprocessorSymbols that apply to the session
LogDirectory or log settings
```

A compile request contains:

```text
CompilerProtocolVersion
AssemblyName
DebugBuild
PreprocessorSymbols that apply to the compile
SourceInput[]
CustomReferencePaths[]
```

Each source contains its logical name, bytes, and optional embedded document
path. A response contains success, PE bytes, optional PDB bytes, diagnostics,
and one structured transport or internal error.

### Target references

The worker execution framework does not define the plugin target framework.
The host must send the target runtime and ordered probe directories.

This is required for these combinations:

| Host | Worker | Plugin target references |
| --- | --- | --- |
| Legacy on Windows/Proton | net48 worker | net48 and SE1 |
| Interim on Windows/Proton | net48 worker | host net10 Windows and SE1 |
| Modern on Windows/Proton | net48 worker | host net10 Windows and SE2 |
| Interim on native Linux | net10 worker | native host and SE1 compatibility set |
| Modern on native Linux | net10 worker | native host and SE2 compatibility set |

The Windows net48 worker must not use its own framework directory when
compiling an Interim or Modern plugin. The net10 launcher supplies its runtime
directory first, preserving the current namespace-clash avoidance behavior.

### Worker targets and packaging

Use one OS-conditional `Compiler.csproj`:

```text
Windows build -> net48 executable
Linux build   -> net10.0 executable
```

The worker is framework-dependent. Do not bundle a private net10 runtime on
Windows. Do not make the Windows worker net10 because a Legacy-only installation
may not have net10.

Package the complete worker build output rather than copying selected Roslyn
dependencies. Store it under:

```text
Libraries/Compiler/<compiler-protocol-version>/
```

Protocol-versioned directories allow a partial deployment to add a breaking
worker without replacing the worker used by an older launcher. A full update
stages only the current version and removes obsolete versions through the usual
installation replacement.

### Lifecycle

1. Run update and installation checks before starting the worker.
2. Start one worker for the current plugin-loading session.
3. Exchange a protocol handshake.
4. Initialize target references once.
5. Process compile requests sequentially.
6. Keep the worker alive after ordinary compilation diagnostics.
7. On transport failure or worker exit, fail the current plugin clearly.
8. Allow the next plugin to start a fresh worker; do not automatically retry the
   same compilation.
9. On disposal, close stdin, wait briefly, then terminate if required.
10. Exit the worker on stdin EOF so host crashes do not normally leave orphans.

Drain stderr concurrently and cap retained diagnostic output. Validate frame
lengths before allocation and reject PE/PDB payloads on failed responses.

### Removal after migration

After all launchers use the worker, remove:

- Legacy compiler AppDomain creation and setup.
- Interim and Modern compiler `AssemblyLoadContext` classes.
- Reflection-based compiler wrappers.
- Compiler binding redirects and private probing assumptions.
- Compiler-specific manual dependency copies.
- `MarshalByRefObject` from `RoslynCompiler`.
- The `Shared -> Compiler` assembly reference.

Keep game assembly resolution, plugin dependency resolution, preloaders, and
the SE1 patch that observes the game's own mod compiler. Those are unrelated to
Pulsar's plugin compiler isolation.

## Independent UI Process

The intended UI boundary is one child UI process per launcher, communicating
through `Pulsar.Protocol.UI` over redirected stdio. This is the smallest design
that works on Windows, Proton, and native Linux without introducing named pipe
and Unix socket variants.

The launcher owns application state and game operations. The UI renders state
and returns user intent.

Host responsibilities:

- Build an initial immutable UI state snapshot.
- Validate every command received from the UI.
- Execute updates, profile changes, plugin operations, and game launches.
- Send state changes and operation results.
- Continue safely or report an error when the UI exits unexpectedly.

UI responsibilities:

- Render snapshots and events.
- Send commands containing identifiers and user-entered values.
- Avoid direct access to game assemblies or Shared application services.
- Keep stdout protocol-only and write diagnostics to stderr.

Do not expose mutable configuration classes directly. Define narrow DTOs at the
screen boundary so configuration storage can change without changing the UI
protocol.

### UI runtime gate

Choose the UI framework and target only after deciding whether a Windows user
running exclusively on Legacy/net48 must have the independent UI without
installing net10.

If Legacy-only Windows support is mandatory, a net10-only UI is not acceptable.
The implementation must use one of these concrete options:

| Option | Cost |
| --- | --- |
| net48 Windows UI plus net10 Linux UI | Two platform UI implementations or a framework supporting both |
| Self-contained modern Windows UI | Larger Windows package |
| Keep Legacy UI in-process temporarily | Delays complete UI separation |

Do not choose a speculative abstraction to hide this runtime decision. Build
the protocol and compiler work first; then select the smallest UI option that
meets the confirmed Legacy requirement.

Version the UI protocol independently from the compiler protocol. A UI contract
change must not move the compiler worker directory or invalidate compiler
compatibility.

## Platform Releases and Updater

### Asset selection

Replace substring asset matching with exact platform-aware names:

```text
Pulsar-Windows-v<version>.zip
Pulsar-Linux-v<version>.zip
Updater-v<version>.exe
```

Use the actual process OS for package selection. Under Proton the process is a
Windows process and selects the Windows package. Do not use the current
`Tools.IsNative()` name as an OS check because it distinguishes Proton from
non-Proton, not Windows from Linux.

### Windows and Proton

Keep the net48 updater as the only external updater executable. It downloads
the Windows package, waits for the caller to exit, applies the archive, and
restarts the actual apphost path.

### Native Linux

The native launcher downloads the Linux package and applies it in-process.
Unix permits running files to be unlinked or replaced. After replacement, the
launcher starts the new apphost and exits.

Share only the archive validation and filesystem replacement source between
Shared and Updater. Compile the source into both assemblies so the standalone
`Updater.exe` remains one downloadable file with no new adjacent dependency.

The update sequence must protect user data:

1. Download and hash the complete asset before extraction.
2. Extract into a sibling staging directory.
3. Validate platform, version, and required files.
4. Preserve the root `Legacy`, `Interim`, and `Modern` data directories.
5. Keep a rollback directory until replacement completes.
6. Restore the previous installation on a failed replacement where possible.
7. Restart using the actual process executable and original argument list.
8. Delete the rollback only after a successful replacement and restart handoff.

Do not clean the live directory before the new package is fully downloaded,
extracted, and validated.

## GitHub Actions Integration

The build logic belongs in MSBuild. Workflow YAML should call the same `Stage`
and `Pack` targets used locally.

### Validation workflow

GitHub-hosted runners can build game-independent projects without proprietary
client files:

```text
Pulsar.Protocol
Compiler
Updater
compiler protocol smoke check
```

Full launcher builds remain unavailable until client references are provisioned.

### Full build runners

Future full builds can use protected self-hosted runners with licensed client
installations:

```text
[self-hosted, pulsar, windows]
[self-hosted, pulsar, linux]
```

Do not run pull-request code, especially fork code, on a persistent Steam-equipped
runner. Full builds should run only for protected branches, protected tags, or
manually approved release environments. Ephemeral runners are preferred when
practical.

Pass game locations as runner environment or workflow variables mapped to
`Bin64` and `Game2`. Do not copy game assemblies into the repository or ordinary
workflow artifacts.

### Release workflow

Recommended sequence:

| Job | Output |
| --- | --- |
| Windows stage | Unsigned Windows/Proton stage |
| Linux stage | Native Linux stage |
| Windows signing | Signed Windows stage and standalone updater |
| Windows pack | Windows ZIP |
| Linux pack | Linux ZIP |
| Release | Checksums, attestations, and GitHub release assets |

Build and stage jobs use read-only repository permissions. Only the signing job
receives signing identity. Only the final release job receives `contents: write`.

### Signing

Use Authenticode for Pulsar-owned Windows PE files. Strong-name signing does not
provide publisher trust and is not a substitute.

Sign after staging and before packing:

```text
Legacy.exe
Interim.exe and Pulsar-owned managed entry DLL
Modern.exe and Pulsar-owned managed entry DLL
Updater.exe
Pulsar.Shared.dll copies
Pulsar.Protocol.dll copies
Compiler worker executable
other Pulsar-owned DLLs introduced later
```

Do not sign third-party NuGet DLLs as if Pulsar published them.

Prefer cloud or HSM-backed signing with GitHub OIDC, such as Azure Artifact
Signing. Restrict the federated identity to this repository and a protected
release environment. Grant `id-token: write` only to the signing job and pin
third-party actions to full commit SHAs.

If a PFX must be used, keep the file and password as separate protected secrets,
materialize the certificate only on the ephemeral runner, avoid command-line
password exposure, and delete it after signing.

Generate SHA-256 checksums and GitHub artifact attestations for both archives.
The Linux archive is not an Authenticode target. Add a Linux package signature
only when there is a concrete verification path in the launcher or installer.

## Verification Strategy

There are currently no repository tests. Add only checks that protect the new
non-trivial boundaries.

### Build checks

- `dotnet build Compiler/Compiler.csproj` succeeds without game paths.
- `dotnet build Updater/Updater.csproj` succeeds without game paths.
- `dotnet build Modern/Modern.csproj` requires Game2 but not Bin64.
- A direct Legacy framework build matches the corresponding solution output.
- Windows and Linux full builds remain isolated under `artifacts`.

### Deployment checks

- Deploying Legacy preserves Interim, Modern, and user data.
- Deploying Interim preserves Legacy, Modern, and user data.
- Deploying Modern preserves both SE1 launchers and user data.
- Full staging starts clean and contains no stale files.
- Every staged first-party dependency exists.
- Game assemblies are absent from the package.

### Compiler smoke check

Provide one small runnable integration check that:

1. Starts the worker.
2. Completes the protocol handshake.
3. Initializes basic runtime references.
4. Compiles a trivial release assembly.
5. Compiles a debug assembly and receives a portable PDB.
6. Returns a structured syntax diagnostic.
7. Rejects a mismatched protocol version.
8. Exits on stdin EOF.

Also manually verify one custom NuGet reference and one
`IgnoresAccessChecksTo` publicization case before removing the old compiler.

### Update checks

- Windows and Proton select the Windows archive.
- Native Linux selects the Linux archive.
- A truncated or hash-mismatched archive leaves the installation untouched.
- A staged package missing a required launcher is rejected.
- User data survives both external and in-process updates.
- The updated launcher restarts with the original arguments.

## Implementation Phases

### Phase 1: Build foundation

- Add `global.json`.
- Enable artifacts output.
- Add local props import and platform path fallbacks.
- Add scoped MSBuild validation.
- Normalize x64 and configuration behavior.
- Add net48 reference assemblies support.
- Remove the unused net8 Shared target.
- Document full and partial build commands.

Exit criteria: direct game-independent projects build on both platforms, and
launcher builds reach only their expected game-path validation.

### Phase 2: Cross-platform deployment and packaging

- Add project `Deploy` targets.
- Add `Pulsar.proj` with `Stage` and `Pack`.
- Derive deployed dependencies from resolved MSBuild items.
- Implement platform package manifests.
- Delete batch verification and deployment hooks.

Exit criteria: ordinary builds do not touch an installation, partial deploys
preserve unrelated outputs, and both platform stages are reproducible from a
clean artifacts directory.

### Phase 3: Protocol extraction

- Add `Pulsar.Protocol/netstandard2.0`.
- Add framing and compiler DTOs only.
- Move host compiler interfaces into Shared.
- Replace `Shared -> Compiler` with `Shared -> Protocol`.
- Add explicit Roslyn references only where the unrelated SE1 game-compiler
  patch still requires them.

Exit criteria: current in-process compiler behavior still works while Shared no
longer receives the Compiler implementation transitively.

### Phase 4: Windows compiler worker

- Convert Compiler into a net48 Windows executable.
- Add handshake, initialization, compile loop, and structured diagnostics.
- Add the Shared process-backed compiler facade.
- Switch Legacy, then Interim, then Modern on Windows.
- Package the full worker output under its protocol version.

Exit criteria: all Windows launchers compile representative plugins through the
worker, and a net48 worker correctly compiles net10-targeted plugins.

### Phase 5: Native Linux compiler worker

- Add the OS-conditional net10 compiler target.
- Build and package it on Linux.
- Switch native Interim and Modern to the worker.
- Verify reference ordering against the native compatibility environment.
- Remove old AppDomain, load-context, and reflection compiler isolation.

Exit criteria: Windows/Proton use the net48 worker, native Linux uses the net10
worker, and no launcher loads `Pulsar.Compiler` into the game process.

### Phase 6: Platform updater split

- Introduce exact platform asset names.
- Share archive application source between Shared and Updater.
- Add staged, validated, rollback-capable updates.
- Keep the external updater on Windows/Proton.
- Add in-process replacement and restart on native Linux.

Exit criteria: each platform updates from its own archive without bundling a
second external updater executable.

### Phase 7: Independent UI

- Confirm the Legacy-only Windows runtime requirement.
- Select the UI runtime/framework from that constraint.
- Add only the UI protocol messages needed by the first migrated screen.
- Launch one UI child over framed stdio.
- Move state ownership and command validation into the launcher.
- Migrate screens incrementally rather than duplicating the full current UI.

Exit criteria: the first useful UI workflow runs out of process, handles child
exit cleanly, and does not reference game assemblies.

### Phase 8: CI and signing

- Add hosted game-independent validation.
- Add protected Windows and Linux full-build jobs when runners are available.
- Add protected Authenticode signing for the Windows stage.
- Generate checksums and attestations.
- Upload exact platform assets and standalone updater.

Exit criteria: a protected release tag produces signed Windows artifacts,
validated Linux artifacts, and release names consumed exactly by the updater.

## Main Risks

| Risk | Mitigation |
| --- | --- |
| Linux Windows-targeting output differs from Windows | Build each release on its target OS |
| net48 worker resolves its own BCL for net10 plugins | Host supplies ordered target runtime probe directories |
| Partial deploy breaks another launcher | Protocol-versioned worker directories and owned subtrees |
| Custom Steam library is not auto-detected | Explicit local props override; add one setup command only if needed |
| Self-hosted runner executes untrusted code | Protected tags/environments and no PR execution |
| Manual deployment misses dependencies | Use resolved copy-local items and validate final manifests |
| Native update damages installation | Download, hash, extract, and validate before replacement; retain rollback |
| UI requires net10 on a Legacy-only Windows system | Resolve the UI runtime gate before selecting the framework |
| Protocol project becomes a dumping ground | DTO-only rule and separate compiler/UI namespaces and versions |

## Expected End State

```text
dotnet build
  -> isolated artifacts only

project Deploy
  -> updates one owned launcher subtree

Pulsar.proj Stage
  -> clean complete platform installation tree

Pulsar.proj Pack
  -> one Windows/Proton ZIP or one native Linux ZIP

Launcher
  -> owns state, downloads, NuGet, caches, assembly loading, and UI commands
  -> starts compiler worker for startup compilation
  -> starts independent UI when that phase is implemented

Pulsar.Protocol
  -> framing plus independently versioned Compiler and UI DTOs

Compiler worker
  -> Roslyn, Cecil publicization, PE/PDB output, diagnostics

Updater
  -> one external net48 executable on Windows/Proton
  -> shared in-process application logic on native Linux
```

This structure keeps the normal build predictable, partial work practical,
release assembly explicit, process boundaries small, and future CI focused on
orchestration rather than rebuilding repository logic in workflow YAML.
