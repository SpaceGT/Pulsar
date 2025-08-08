using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Pulsar.Shared.Data;

namespace Pulsar.Shared.Config
{
    public class ProfilesConfig(string folderPath)
    {
        private const string currentKey = "Current";
        private readonly Dictionary<string, Profile> profiles = [];

        public Profile Current { get; private set; }
        public IEnumerable<Profile> Profiles => profiles.Values;

        public void Save(string key = null)
        {
            Profile profile;
            if (key == null)
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

                try
                {
                    using FileStream fs = File.OpenRead(file);
                    Profile profile = (Profile)serializer.Deserialize(fs);
                    config.profiles[profile.Key] = profile;
                }
                catch (Exception e)
                {
                    LogFile.Error($"An error occurred while loading profile " + name + ": " + e);
                }
            }

            string path = Path.Combine(folderPath, currentKey + ".xml");
            try
            {
                using FileStream fs = File.OpenRead(path);
                config.Current = (Profile)serializer.Deserialize(fs);
            }
            catch (Exception e)
            {
                LogFile.Error($"An error occurred while loading current plugins: " + e);

                if (File.Exists(path))
                    File.Move(path, path + ".bak");

                config.Current = new Profile(currentKey, []);
            }

            return config;
        }
    }
}
