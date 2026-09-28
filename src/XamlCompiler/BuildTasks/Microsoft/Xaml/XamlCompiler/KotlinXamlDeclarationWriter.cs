// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Markup.Compiler.MSBuildInterop;
using Microsoft.UI.Xaml.Markup.Compiler.XamlDom;

namespace Microsoft.UI.Xaml.Markup.Compiler
{
    internal static class KotlinXamlDeclarationWriter
    {
        // This is a serialization view over the compiler DOM/harvester, not a second XAML parser.
        internal static void ValidateTree(XamlDomObject root)
        {
            foreach (var node in new XamlDomIterator(root).DescendantsAndSelf())
            {
                if (node.IsGetObject) continue;
                if (node.Type.IsUnknown || node.Type.UnderlyingType == null)
                    throw new NotSupportedException($"Kotlin XAML ({node.StartLineNumber},{node.StartLinePosition}): unresolved type {node.Type.Name} requires an application type input.");
                if (node.Type.Name == "DataTemplate" || node.Type.Name == "ControlTemplate")
                    throw new NotSupportedException($"Kotlin XAML ({node.StartLineNumber},{node.StartLinePosition}): template scopes are not implemented yet.");
                foreach (var member in node.MemberNodes)
                {
                    if (member.Member.IsUnknown)
                        throw new NotSupportedException($"Kotlin XAML ({member.StartLineNumber},{member.StartLinePosition}): unresolved member {member.Member.Name}.");
                    if (member.Member.IsDirective && new[] { "Load", "DeferLoadStrategy", "DataType", "Phase", "Properties", "DefaultBindMode" }.Contains(member.Member.Name))
                        throw new NotSupportedException($"Kotlin XAML ({member.StartLineNumber},{member.StartLinePosition}): x:{member.Member.Name} is not implemented yet.");
                }
            }
        }

        internal static KotlinXamlDeclarationIndex Create(IEnumerable<XamlClassCodeInfo> classes, IEnumerable<string> resources)
        {
            var result = new KotlinXamlDeclarationIndex();
            result.Resources = resources.Select(ResourcePath).OrderBy(x => x, StringComparer.Ordinal).ToList();
            foreach (var info in classes.OrderBy(x => x.ClassName.FullName, StringComparer.Ordinal))
            {
                if (info.PerXamlFileInfo.Count != 1)
                    throw new NotSupportedException($"Kotlin XAML: {info.ClassName.FullName} must have exactly one XAML file.");
                var file = info.PerXamlFileInfo.Single();
                var page = new KotlinXamlPageDeclaration {
                    ClassName = info.ClassName.FullName, ResourcePath = ResourcePath(file.ApparentRelativePath),
                    BaseTypeName = info.BaseTypeName, IsApplication = info.IsApplication
                };
                foreach (var element in file.ConnectionIdElements.OrderBy(x => x.ConnectionId))
                {
                    if (element.HasBindAssignments || element.HasBoundEventAssignments || element.IsTemplateChild || element.CanBeInstantiatedLater)
                        throw new NotSupportedException($"Kotlin XAML {page.ResourcePath}({element.LineNumberInfo.StartLineNumber}): binding, template and deferred loading support is not implemented yet.");
                    var connection = new KotlinXamlConnectionDeclaration {
                        Id = element.ConnectionId, TypeName = element.Type?.UnderlyingType?.FullName,
                        FieldName = element.FieldDefinition?.FieldName, Location = Location(element.LineNumberInfo)
                    };
                    if (String.IsNullOrEmpty(connection.TypeName))
                        throw new NotSupportedException($"Kotlin XAML: unresolved connection type in {page.ResourcePath}.");
                    foreach (var assignment in element.EventAssignments.OrderBy(x => x.EventName, StringComparer.Ordinal))
                        connection.Events.Add(new KotlinXamlEventDeclaration {
                            Name = assignment.EventName, HandlerName = assignment.HandlerName,
                            DeclaringTypeName = assignment.DeclaringType.StandardName,
                            DelegateTypeName = assignment.EventType.StandardName, Location = Location(assignment.LineNumberInfo)
                        });
                    page.Connections.Add(connection);
                }
                if (page.Connections.Any(x => x.FieldName != null)) page.Features.Add("named-elements");
                if (page.Connections.Any(x => x.Events.Count != 0)) page.Features.Add("events");
                result.Pages.Add(page);
            }
            return result;
        }

        private static KotlinXamlSourceLocation Location(LineNumberInfo location) =>
            new KotlinXamlSourceLocation { Line = location.StartLineNumber, Column = location.StartLinePosition };

        private static string ResourcePath(string path)
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("/") || normalized.Contains(":") || normalized.Split('/').Any(x => x == ".."))
                throw new ArgumentException($"Kotlin XAML resource path must be project-relative: {path}");
            return normalized;
        }
    }
}
