using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Pulsar.Shared.Data;

namespace Pulsar.Shared.Config;

public class ProfilesConfig(string folderPath)
{
    private const string currentKey = "Current";
    private readonly Dictionary<string, Profile> profiles = [];

    public Profile Current { get; private set; }
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
            XmlSerializer serializer = new(typeof(Profile));

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
        XmlSerializer serializer = new(typeof(Profile));

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        foreach (string file in Directory.GetFiles(folderPath))
        {
            string name = Path.GetFileName(file);
            if (name == currentKey + ".xml" || name.EndsWith(".bak"))
                continue;

            Profile profile = null;
            using FileStream fs = File.OpenRead(file);

            try
            {
                profile = (Profile)serializer.Deserialize(fs);
            }
            catch (XmlException) { }
            catch (InvalidOperationException) { }

            if (profile?.Validate() == true)
                config.profiles[profile.Key] = profile;
            else
                LogFile.Error("An error occurred while loading profile " + name);
        }

        {
            Profile current = null;
            string file = Path.Combine(folderPath, currentKey + ".xml");

            if (File.Exists(file))
            {
                using FileStream fs = File.OpenRead(file);

                try
                {
                    current = (Profile)serializer.Deserialize(fs);
                }
                catch (XmlException) { }
                catch (InvalidOperationException) { }
            }

            if (current?.Validate() == true)
                config.Current = current;
            else
            {
                LogFile.Error($"An error occurred while loading the {currentKey} profile");
                config.Current = new Profile(currentKey);

                if (File.Exists(file))
                {
                    int backupCount = Directory
                        .EnumerateFiles(folderPath)
                        .Where(file => Path.GetExtension(file).Contains(".bak"))
                        .Count();

                    string suffix = ".bak";
                    if (backupCount > 0)
                        suffix += backupCount;

                    File.Move(file, file + suffix);

                    string message =
                        "The current profile could not be loaded!\n"
                        + "The list of enabled plugins has been reset.\n\n"
                        + $"The original profile has been saved to Profiles\\{currentKey}.xml{suffix}";
                    Tools.ShowMessageBox(message, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        return config;
    }
}
