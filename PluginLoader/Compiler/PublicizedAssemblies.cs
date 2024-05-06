using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace avaness.PluginLoader.Compiler;

public class PublicizedAssemblies
{
    private readonly HashSet<string> ignoredAssemblyNames = new HashSet<string>();
    private readonly Dictionary<string, MetadataReference> publicizedReferences = new Dictionary<string, MetadataReference>();

    public void InspectSource(SourceText source)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        SyntaxNode root = syntaxTree.GetRoot();

        IEnumerable<AttributeSyntax> attributes = root
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString().EndsWith("IgnoresAccessChecksTo"));

        foreach (var attribute in attributes)
        {
            AttributeArgumentSyntax targetAssemblyArg = attribute.ArgumentList.Arguments.FirstOrDefault();

            if (targetAssemblyArg?.Expression is not LiteralExpressionSyntax literalExpression)
                continue;

            if (literalExpression.IsKind(SyntaxKind.StringLiteralExpression))
            {
                string ignoredAssemblyName = literalExpression.Token.ValueText;
                ignoredAssemblyNames.Add(ignoredAssemblyName);
            }
        }
    }

    public MetadataReference PublicizeReferenceIfRequired(string targetName, string dependencyName, MetadataReference dependency)
    {
        if (!ignoredAssemblyNames.Contains(dependencyName))
            return dependency;

        if (!GetPublicizedReference(dependencyName, dependency, out var publicizedReference))
            throw new Exception($"Failed to publicize assembly {dependencyName} for {targetName}");

        LogFile.WriteLine($"Using publicized {dependencyName} for {targetName}");
        return publicizedReference;
    }

    private bool GetPublicizedReference(string referenceName, MetadataReference originalReference, out MetadataReference publicizedRef)
    {
        if (publicizedReferences.TryGetValue(referenceName, out publicizedRef))
            return true;

        if (originalReference is not PortableExecutableReference portableRef || string.IsNullOrEmpty(portableRef.FilePath))
            return false;

        publicizedRef = Publicizer.PublicizeReference(portableRef);
        publicizedReferences.Add(referenceName, publicizedRef);

        return true;
    }
}