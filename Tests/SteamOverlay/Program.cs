// Run on Linux: dotnet run --project Tests/SteamOverlay
using System;
using System.Diagnostics;
using Pulsar.Shared;

if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException("Run this regression check on Linux.");

const string enable = "ENABLE_VK_LAYER_VALVE_steam_overlay_1";
const string disable = "DISABLE_VK_LAYER_VALVE_steam_overlay_1";

void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

Environment.SetEnvironmentVariable(enable, "1");
Environment.SetEnvironmentVariable(disable, null);
Environment.SetEnvironmentVariable("SDL_VIDEO_DRIVER", "wayland");

const string preload =
    ":/steam/ubuntu12_32/gameoverlayrenderer.so:/steam/ubuntu12_64/gameoverlayrenderer.so libkeep.so";
Environment.SetEnvironmentVariable("LD_PRELOAD", preload);
Environment.SetEnvironmentVariable("SteamGameId", "244850");
using (Process child = new())
{
    child.StartInfo = new ProcessStartInfo("/usr/bin/env")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = "/tmp",
    };
    SteamOverlay.DisableForHelper(child.StartInfo);
    Check(child.StartInfo.Environment["LD_PRELOAD"] == "libkeep.so", "Preserve unrelated preloads");
    Check(child.StartInfo.Environment["SteamGameId"] == "244850", "Preserve game identity");
    Check(
        child.StartInfo.Environment["SDL_VIDEO_DRIVER"] == "wayland",
        "Preserve Wayland selection"
    );
    Check(child.StartInfo.WorkingDirectory == "/tmp", "Preserve helper launch path");
    Check(Environment.GetEnvironmentVariable("LD_PRELOAD") == preload, "Keep parent injection");
    Check(Environment.GetEnvironmentVariable(enable) == "1", "Keep parent Vulkan overlay");
    Check(Environment.GetEnvironmentVariable(disable) == null, "Do not disable parent overlay");
    // The unrelated fake library need not exist to check the child's actual environment.
    child.Start();
    string output = child.StandardOutput.ReadToEnd();
    child.StandardError.ReadToEnd();
    child.WaitForExit();
    Check(child.ExitCode == 0, "Helper starts successfully");
    Check(output.Contains("LD_PRELOAD=libkeep.so\n"), "Child retains unrelated preload");
    Check(output.Contains(disable + "=1\n"), "Child disables Vulkan overlay");
    Check(!output.Contains(enable + "="), "Child has no Vulkan overlay opt-in");
    Check(!output.Contains("gameoverlayrenderer.so"), "Child has no overlay injection");
}

foreach (string value in new[] { "gameoverlayrenderer.so", "", null })
{
    ProcessStartInfo startInfo = new();
    if (value == null)
        startInfo.Environment.Remove("LD_PRELOAD");
    else
        startInfo.Environment["LD_PRELOAD"] = value;
    SteamOverlay.DisableForHelper(startInfo);
    Check(!startInfo.Environment.ContainsKey("LD_PRELOAD"), "Handle empty or overlay-only preload");
}

Console.WriteLine("Steam overlay regression checks passed.");
