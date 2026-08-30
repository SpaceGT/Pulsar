using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Pulsar.Shared.Splash;

public sealed class ProgressTracker
{
    private static readonly object stateLock = new();
    private static ProgressTracker instance;

    private readonly Dictionary<MethodBase, float> goals = [];
    private readonly Harmony harmony;

    private float progress;

    public ProgressTracker(string harmonyId)
    {
        harmony = new Harmony(harmonyId);
        lock (stateLock)
            instance = this;
    }

    internal static void ClearActive()
    {
        lock (stateLock)
            instance = null;
    }

    public void Patch(string typeName, string methodName, float goal, bool prefix = false)
    {
        try
        {
            Type type = Type.GetType(typeName, true);
            MethodInfo method =
                AccessTools.DeclaredMethod(type, methodName)
                ?? throw new MissingMethodException(type.FullName, methodName);

            HarmonyMethod patch = new(AccessTools.Method(typeof(ProgressTracker), nameof(Report)));
            harmony.Patch(method, prefix: prefix ? patch : null, postfix: prefix ? null : patch);
            goals.Add(method, goal);
        }
        catch (Exception e)
        {
            LogFile.Warn($"Progress target unavailable: {typeName}.{methodName}: {e.Message}");
        }
    }

    private static void Report(MethodBase __originalMethod)
    {
        try
        {
            lock (stateLock)
                instance?.Advance(__originalMethod);
        }
        catch (Exception e)
        {
            LogFile.Error("Progress callback failed: " + e);
        }
    }

    private void Advance(MethodBase method)
    {
        if (!goals.TryGetValue(method, out float goal) || goal <= progress)
            return;

        progress = goal;
        SplashManager.Instance?.SetBarValue(goal);
    }
}
