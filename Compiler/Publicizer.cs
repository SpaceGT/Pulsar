using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace Pulsar.Compiler
{
    internal static class Publicizer
    {
        public static MetadataReference PublicizeReference(PortableExecutableReference reference)
        {
            using var assembly = AssemblyDefinition.ReadAssembly(reference.FilePath);
            PublicizeAssembly(assembly);

            var stream = new MemoryStream();
            assembly.Write(stream);
            stream.Position = 0;

            return MetadataReference.CreateFromStream(stream);
        }

        private static void PublicizeAssembly(AssemblyDefinition assembly)
        {
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.GetTypes())
                {
                    TryPublicizeType(type);

                    foreach (var field in type.Fields)
                    {
                        TryPublicizeField(field);
                    }

                    foreach (var method in type.Methods)
                    {
                        TryPublicizeMethod(method);
                    }

                    foreach (var property in type.Properties)
                    {
                        TryPublicizeProperty(property);
                    }
                }
            }
        }

        private static bool TryPublicizeType(TypeDefinition type)
        {
            if (IsCompilerGenerated(type) || type.IsPublic)
            {
                return false;
            }

            bool isNested =
                type.IsNested
                || type.IsNestedAssembly
                || type.IsNestedFamilyOrAssembly
                || type.IsNestedFamilyAndAssembly;

            if (isNested)
            {
                type.IsNestedPublic = true;
            }
            else
            {
                type.IsPublic = true;
            }

            return true;
        }

        private static bool TryPublicizeField(FieldDefinition field)
        {
            if (IsCompilerGenerated(field))
            {
                return false;
            }

            bool shouldPublicize =
                field.IsPrivate
                || field.IsAssembly
                || field.IsFamily
                || field.IsFamilyOrAssembly
                || field.IsFamilyAndAssembly;

            if (shouldPublicize)
            {
                field.IsPublic = true;
            }

            return shouldPublicize;
        }

        private static bool TryPublicizeMethod(MethodDefinition method, bool force = false)
        {
            if (!force && (IsCompilerGenerated(method) || method.IsVirtual))
            {
                return false;
            }

            bool shouldPublicize =
                method.IsPrivate
                || method.IsAssembly
                || method.IsFamily
                || method.IsFamilyOrAssembly
                || method.IsFamilyAndAssembly;

            if (shouldPublicize)
            {
                method.IsPublic = true;
            }

            return shouldPublicize;
        }

        private static bool TryPublicizeProperty(PropertyDefinition property)
        {
            if (IsCompilerGenerated(property))
            {
                return false;
            }

            if (property.GetMethod is MethodDefinition getter)
            {
                TryPublicizeMethod(getter, force: true);
            }

            if (property.SetMethod is MethodDefinition setter)
            {
                TryPublicizeMethod(setter, force: true);
            }

            return true;
        }

        private static bool IsCompilerGenerated(IMemberDefinition member)
        {
            string compilerGenerated = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
            return member.CustomAttributes.Any(attr =>
                attr.AttributeType.FullName == compilerGenerated
            );
        }
    }
}
