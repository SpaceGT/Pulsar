using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Pulsar.Compiler
{
    public class ReferenceHelper
    {
        public static readonly HashSet<string> ReferenceBlacklist = ["System.ValueTuple"];

        public static List<string> GetAssemblies(AppDomain appDomain)
        {
            return [.. appDomain.GetAssemblies().Where(IsValidReference).Select(x => x.FullName)];
        }

        public static bool IsValidReference(Assembly a)
        {
            string name = a.GetName().Name;
            return !a.IsDynamic
                && !string.IsNullOrWhiteSpace(a.Location)
                && !ReferenceBlacklist.Contains(name)
                && !name.Contains("Pulsar") // Please send PRs for Pulsar patches :)
                && !name.Contains("CodeAnalysis"); // We use a more modern version then SE
        }
    }
}
