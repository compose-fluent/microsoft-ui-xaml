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
        internal static void ValidateSymbols(IEnumerable<XamlClassCodeInfo> classes,
            KotlinXamlDeclarationIndex declarations, KotlinXamlSemanticSymbols symbols)
        {
            if (symbols.SchemaVersion != 1 || symbols.Declarations == null || symbols.Pages == null ||
                symbols.DeclarationFingerprint == null || symbols.DeclarationFingerprint.Length != 64)
                throw new ArgumentException("Invalid Kotlin XAML semantic symbol protocol.");
            if (!DeclarationTokens(declarations).SequenceEqual(DeclarationTokens(symbols.Declarations), StringComparer.Ordinal))
                throw new ArgumentException("Kotlin XAML declarations changed after semantic compilation; rebuild the declaration input.");
            if (symbols.Pages.Count != declarations.Pages.Count || symbols.Pages.Select(x => x.ClassName).Distinct().Count() != symbols.Pages.Count)
                throw new ArgumentException("Kotlin XAML semantic page set does not match declarations.");
            foreach (var info in classes)
            {
                var page = symbols.Pages.SingleOrDefault(x => x.ClassName == info.ClassName.FullName);
                if (page?.Handlers == null) throw new ArgumentException($"Missing Kotlin symbols for {info.ClassName.FullName}.");
                foreach (var assignment in info.PerXamlFileInfo.SelectMany(x => x.ConnectionIdElements).SelectMany(x => x.EventAssignments))
                {
                    var handlers = page.Handlers.Where(x => x.Name == assignment.HandlerName).ToList();
                    var invoke = assignment.EventType.UnderlyingType.GetMethod("Invoke");
                    if (handlers.Count != 1 || invoke == null || handlers[0].ReturnTypeName != invoke.ReturnType.FullName ||
                        handlers[0].ParameterTypeNames == null ||
                        !handlers[0].ParameterTypeNames.SequenceEqual(invoke.GetParameters().Select(x => x.ParameterType.FullName)))
                        throw new ArgumentException($"Kotlin XAML {info.ClassName.FullName}({assignment.LineNumberInfo.StartLineNumber},{assignment.LineNumberInfo.StartLinePosition}): handler {assignment.HandlerName} does not match {assignment.EventType.StandardName}.");
                }
            }
        }

        // Shared compiler builds do not depend on the executable's JSON serializer.
        // Compare protocol values directly, including list boundaries and source positions.
        private static IEnumerable<string> DeclarationTokens(KotlinXamlDeclarationIndex index)
        {
            yield return index.SchemaVersion.ToString();
            yield return index.Resources.Count.ToString();
            foreach (var resource in index.Resources) yield return resource;
            yield return index.Pages.Count.ToString();
            foreach (var page in index.Pages)
            {
                yield return page.ClassName; yield return page.ResourcePath; yield return page.BaseTypeName;
                yield return page.IsApplication.ToString(); yield return page.Features.Count.ToString();
                foreach (var feature in page.Features) yield return feature;
                yield return page.Connections.Count.ToString();
                foreach (var connection in page.Connections)
                {
                    yield return connection.Id.ToString(); yield return connection.TypeName; yield return connection.FieldName;
                    yield return connection.Location.Line.ToString(); yield return connection.Location.Column.ToString();
                    yield return connection.Events.Count.ToString();
                    foreach (var assignment in connection.Events)
                    {
                        yield return assignment.Name; yield return assignment.HandlerName;
                        yield return assignment.DeclaringTypeName; yield return assignment.DelegateTypeName;
                        yield return assignment.Location.Line.ToString(); yield return assignment.Location.Column.ToString();
                    }
                }
            }
        }
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
                page.Features.Sort(StringComparer.Ordinal);
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
