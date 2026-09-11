# Rewriter owner lifecycle

Run with the .NET 10 SDK:

```sh
dotnet run --project Tests/RewriterLifecycle/RewriterLifecycle.csproj -c Release
```

Links the production `PluginInstance` and `Patch_Rewriter` sources. Game/host
services are stubbed; reflection and the rewriter dispatch use real code.
Checks registration before plugin construction, single dispatch after normal
initialization, disposal, early exceptions and invalid returns staying disabled,
and cleanup after constructor failure. No game installation is required.

This does not install Harmony patches or test the full game startup order.
