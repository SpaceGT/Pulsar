using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace Pulsar.Shared.Network
{
    public class NuGetPackage
    {
        private readonly string installPath;
        private readonly NuGetFramework targetFramework;

        public Item[] LibFiles { get; private set; }
        public Item[] ContentFiles { get; private set; }

        public NuGetPackage(string installPath, NuGetFramework targetFramework)
        {
            this.installPath = installPath;
            this.targetFramework = targetFramework;
            GetFileLists();
        }

        private void GetFileLists()
        {
            PackageFolderReader packageReader = new(installPath);
            FrameworkReducer frameworkReducer = new();
            LibFiles = GetItems(
                packageReader.GetLibItems(),
                frameworkReducer,
                targetFramework,
                false
            );
            ContentFiles = GetItems(
                packageReader.GetContentItems(),
                frameworkReducer,
                targetFramework,
                true
            );
        }

        private Item[] GetItems(
            IEnumerable<FrameworkSpecificGroup> itemGroups,
            FrameworkReducer frameworkReducer,
            NuGetFramework targetFramework,
            bool contentItems
        )
        {
            NuGetFramework nearest = frameworkReducer.GetNearest(
                targetFramework,
                itemGroups.Select(x => x.TargetFramework)
            );
            if (nearest is not null)
            {
                List<Item> libFiles = [];
                foreach (
                    FrameworkSpecificGroup group in itemGroups.Where(x =>
                        x.TargetFramework.Equals(nearest)
                    )
                )
                    libFiles.AddRange(
                        group
                            .Items.Select(x =>
                                GetPackageItem(x, group.TargetFramework, contentItems)
                            )
                            .Where(x => x is not null)
                    );
                return [.. libFiles];
            }

            return [];
        }

        private Item GetPackageItem(string path, NuGetFramework framework, bool content)
        {
            string fullPath = Path.GetFullPath(Path.Combine(installPath, path));
            if (!File.Exists(fullPath))
                return null;

            if (
                TrySplitPath(
                    fullPath,
                    framework.GetShortFolderName(),
                    out string folder,
                    out string file
                )
            )
                return new Item(file, folder);

            if (TrySplitPath(fullPath, content ? "content" : "lib", out folder, out file))
                return new Item(file, folder);

            return null;
        }

        private bool TrySplitPath(
            string fullPath,
            string lastFolderName,
            out string folder,
            out string file
        )
        {
            folder = null;
            file = null;

            int index = fullPath.IndexOf(lastFolderName);
            if (index < 0 || fullPath.Length <= index + lastFolderName.Length + 2)
                return false;

            folder = fullPath.Substring(0, index + lastFolderName.Length);
            file = fullPath.Substring(folder.Length + 1);
            return true;
        }

        public class Item
        {
            public Item(string path, string folder)
            {
                FilePath = path;
                Folder = folder;
                FullPath = Path.Combine(Folder, FilePath);
            }

            public string FilePath { get; set; }
            public string Folder { get; set; }
            public string FullPath { get; set; }
        }
    }
}
