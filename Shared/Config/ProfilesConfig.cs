using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Pulsar.Protocol.Interface;
using Pulsar.Shared.Arguments;
using Pulsar.Shared.Data;

namespace Pulsar.Shared.Config;

public class ProfilesConfig(string folderPath)
{
    private const string currentKey = "Current";
    private readonly XmlSerializer serializer = new(typeof(Profile));
    private readonly Dictionary<string, Profile> profiles = [];

    public Profile Current { get; set; }
    public IEnumerable<Profile> Profiles => profiles.Values;

    public void Save(string key = null)
    {
        Profile profile;
        if (key is null)
            profile = Current;
        else
            profile = profiles[key];

        try
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string path = Path.Combine(folderPath, profile.Key + ".xml");

            if (File.Exists(path))
                File.Delete(path);

            using FileStream fs = File.OpenWrite(path);
            serializer.Serialize(fs, profile);
        }
        catch (Exception e)
        {
            LogFile.Error($"An error occurred while saving profile " + profile.Name + ": " + e);
        }
    }

    public bool Exists(string key) => profiles.ContainsKey(key) || key == currentKey;

    public void Add(Profile profile)
    {
        profiles[profile.Key] = profile;
        Save(profile.Key);
    }

    public void Remove(string key)
    {
        profiles.Remove(key);
        string path = Path.Combine(folderPath, key + ".xml");
        File.Delete(path);
    }

    public void Rename(string key, string newName)
    {
        Profile profile = profiles[key];
        profiles.Remove(key);

        File.Delete(Path.Combine(folderPath, key + ".xml"));

        profile.Name = newName;
        profiles[profile.Key] = profile;

        Save(profile.Key);
    }

    public static ProfilesConfig Load(string mainDirectory)
    {
        LogFile.WriteLine("Loading profiles");

        string folderPath = Path.Combine(mainDirectory, "Profiles");
        ProfilesConfig config = new(folderPath);

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        if (config.TryLoadOverride())
            return config;

        config.LoadProfiles();
        config.LoadCurrent();

        return config;
    }

    private bool TryLoadOverride()
    {
        if (Flags.Current.Profile is not string profileArg)
            return false;

        string file = ResolveProfile(profileArg);
        Profile resolved = Deserialize(file);

        if (resolved?.Validate() != true)
        {
            string message = $"The specified profile '{profileArg}' could not be loaded.";
            LogFile.Error(message);
            Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);
            Environment.Exit(1);
        }

        Current = resolved;
        return true;
    }

    private void LoadProfiles()
    {
        foreach (string file in Directory.GetFiles(folderPath))
        {
            string name = Path.GetFileName(file);
            if (name == currentKey + ".xml" || IsBackup(file))
                continue;

            Profile profile = Deserialize(file);

            if (profile?.Validate() == true)
                profiles[profile.Key] = profile;
            else
                LogFile.Error("An error occurred while loading profile " + name);
        }
    }

    private void LoadCurrent()
    {
        string file = Path.Combine(folderPath, currentKey + ".xml");
        Profile current = Deserialize(file);

        if (current?.Validate() == true)
            Current = current;
        else
        {
            Current = new Profile(currentKey);

            if (File.Exists(file))
                BackupProfile(currentKey, file);
            else
                Save();
        }
    }

    private void BackupProfile(string key, string file)
    {
        LogFile.Error($"An error occurred while loading the {key} profile");

        string suffix = ".bak";
        for (int index = 1; File.Exists(file + suffix); index++)
            suffix = $".bak{index}";

        File.Move(file, file + suffix);

        string path = Path.Combine("Profiles", key + ".xml" + suffix);
        string message =
            "The current profile could not be loaded!\n"
            + "The list of enabled plugins has been reset.\n\n"
            + $"The original profile has been saved to {path}";

        Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Warning);
    }

    private string ResolveProfile(string locator)
    {
        if (Path.IsPathRooted(locator))
            return File.Exists(locator) ? locator : null;

        string file = Path.Combine(folderPath, locator);
        if (File.Exists(file))
            return file;

        foreach (string path in Directory.EnumerateFiles(folderPath))
        {
            if (IsBackup(path))
                continue;

            if (Deserialize(path)?.Name == locator)
                return path;
        }

        return null;
    }

    private static bool IsBackup(string file)
    {
        string extension = Path.GetExtension(file);
        return extension.StartsWith(".bak", StringComparison.OrdinalIgnoreCase)
            && extension.Skip(4).All(char.IsDigit);
    }

    private Profile Deserialize(string file)
    {
        if (!File.Exists(file))
            return null;

        using FileStream fs = File.OpenRead(file);

        try
        {
            return (Profile)serializer.Deserialize(fs);
        }
        catch (XmlException) { }
        catch (InvalidOperationException) { }

        return null;
    }
}
