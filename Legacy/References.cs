using System.Collections.Generic;
using Pulsar.Shared;

namespace Pulsar.Legacy
{
    internal class References
    {
        private static readonly string[] includeGlobs =
        [
            "SpaceEngineers*.dll",
            "VRage*.dll",
            "Sandbox*.dll",
            "ProtoBuf*.dll",
        ];

        private static readonly string[] excludeGlobs = ["VRage.Native.dll"];

        public static HashSet<string> GetReferences(string exeLocation) =>
            Tools.GetFiles(exeLocation, includeGlobs, excludeGlobs);
    }
}
