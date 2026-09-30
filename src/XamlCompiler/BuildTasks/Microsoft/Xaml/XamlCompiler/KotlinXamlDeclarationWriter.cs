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
            if (symbols.SchemaVersion != declarations.SchemaVersion || symbols.Declarations == null || symbols.Pages == null ||
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
                    yield return connection.ElementName; yield return connection.ScopeId.ToString();
                    yield return connection.IsScopeRoot.ToString(); yield return connection.IsTemplateChild.ToString();
                    yield return connection.DataTypeName;
                    yield return connection.Phase.ToString(); yield return connection.CanBeInstantiatedLater.ToString();
                    yield return connection.IsUnloadableRoot.ToString(); yield return connection.Children.Count.ToString();
                    foreach (var child in connection.Children) yield return child.ToString();
                    yield return connection.Location.Line.ToString(); yield return connection.Location.Column.ToString();
                    yield return connection.Events.Count.ToString();
                    foreach (var assignment in connection.Events)
                    {
                        yield return assignment.Name; yield return assignment.HandlerName;
                        yield return assignment.DeclaringTypeName; yield return assignment.DelegateTypeName;
                        yield return assignment.Location.Line.ToString(); yield return assignment.Location.Column.ToString();
                    }
                    yield return connection.Bindings.Count.ToString();
                    foreach (var binding in connection.Bindings)
                    {
                        yield return binding.Name; yield return binding.DeclaringTypeName; yield return binding.TypeName;
                        yield return binding.Mode; yield return binding.IsAttachable.ToString(); yield return binding.IsEvent.ToString();
                        yield return binding.IsLoad.ToString(); yield return binding.Phase.ToString();
                        foreach (var token in ExpressionTokens(binding.Expression)) yield return token;
                        foreach (var token in ExpressionTokens(binding.BindBack)) yield return token;
                        foreach (var token in ExpressionTokens(binding.FallbackValue)) yield return token;
                        foreach (var token in ExpressionTokens(binding.TargetNullValue)) yield return token;
                        yield return binding.Converter; yield return binding.ConverterParameter; yield return binding.ConverterLanguage;
                        yield return binding.UpdateSourceTrigger;
                        yield return binding.Location.Line.ToString(); yield return binding.Location.Column.ToString();
                    }
                }
            }
        }
        private static IEnumerable<string> ExpressionTokens(KotlinXamlBindingExpression expression)
        {
            yield return (expression != null).ToString();
            if (expression == null) yield break;
            yield return expression.Kind; yield return expression.Name; yield return expression.TypeName; yield return expression.Value;
            foreach (var token in ExpressionTokens(expression.Receiver)) yield return token;
            yield return expression.Arguments.Count.ToString();
            foreach (var argument in expression.Arguments)
                foreach (var token in ExpressionTokens(argument)) yield return token;
        }
        // This is a serialization view over the compiler DOM/harvester, not a second XAML parser.
        internal static void ValidateTree(XamlDomObject root)
        {
            foreach (var node in new XamlDomIterator(root).DescendantsAndSelf())
            {
                if (node.IsGetObject) continue;
                if (node.Type.IsUnknown || node.Type.UnderlyingType == null)
                    throw new NotSupportedException($"Kotlin XAML ({node.StartLineNumber},{node.StartLinePosition}): unresolved type {node.Type.Name} requires an application type input.");
                foreach (var member in node.MemberNodes)
                {
                    // Compiler-only directives are intentionally unknown to the runtime
                    // schema. Their values are validated by XamlDomValidator and harvested
                    // into the binding universe before the Kotlin declaration export.
                    if (member.Member.IsUnknown && !DomHelper.IsDataTypeMember(member) && !DomHelper.IsDefaultBindModeMember(member) &&
                        !DomHelper.IsPhaseMember(member) && !DomHelper.IsLoadMember(member) && !DomHelper.IsDeferLoadStrategyMember(member))
                        throw new NotSupportedException($"Kotlin XAML ({member.StartLineNumber},{member.StartLinePosition}): unresolved member {member.Member.Name}.");
                    if (member.Member.IsDirective && member.Member.Name == "Properties")
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
                    var connection = new KotlinXamlConnectionDeclaration {
                        Id = element.ConnectionId, TypeName = element.Type?.UnderlyingType?.FullName,
                        FieldName = element.FieldDefinition?.FieldName, ElementName = element.ElementName,
                        ScopeId = element.BindUniverse.RootElement.ConnectionId, IsScopeRoot = element.IsBindingRoot,
                        // A DataTemplate's first child is a new binding root itself.
                        // ConnectionIdElement.IsTemplateChild only identifies universes rooted
                        // at a FrameworkTemplate, so IsFileRoot owns the complete scope split.
                        IsTemplateChild = !element.BindUniverse.IsFileRoot,
                        // The file root's local CLR type is deliberately unresolved in pass 1.
                        // Its identity is already owned by x:Class and must stay stable in pass 2.
                        DataTypeName = element.BindUniverse.IsFileRoot ? info.ClassName.FullName : element.BindUniverse.DataRootType?.UnderlyingType?.FullName,
                        Phase = element.PhaseAssignment?.Phase ?? 0,
                        CanBeInstantiatedLater = element.CanBeInstantiatedLater, IsUnloadableRoot = element.IsUnloadableRoot,
                        Children = element.AllChildren.Select(x => x.ConnectionId).Distinct().OrderBy(x => x).ToList(),
                        Location = Location(element.LineNumberInfo)
                    };
                    if (String.IsNullOrEmpty(connection.TypeName))
                        throw new NotSupportedException($"Kotlin XAML: unresolved connection type in {page.ResourcePath}.");
                    foreach (var assignment in element.EventAssignments.OrderBy(x => x.EventName, StringComparer.Ordinal))
                        connection.Events.Add(new KotlinXamlEventDeclaration {
                            Name = assignment.EventName, HandlerName = assignment.HandlerName,
                            DeclaringTypeName = assignment.DeclaringType.StandardName,
                            DelegateTypeName = assignment.EventType.StandardName, Location = Location(assignment.LineNumberInfo)
                        });
                    foreach (var assignment in element.BindAssignments.OrderBy(x => x.MemberName, StringComparer.Ordinal))
                        connection.Bindings.Add(Binding(assignment));
                    foreach (var assignment in element.BoundEventAssignments.OrderBy(x => x.MemberName, StringComparer.Ordinal))
                        connection.Bindings.Add(new KotlinXamlBindingDeclaration {
                            Name = assignment.MemberName, DeclaringTypeName = assignment.MemberDeclaringType.UnderlyingType.FullName,
                            TypeName = assignment.MemberType.UnderlyingType.FullName, Mode = "OneTime", IsEvent = true,
                            Expression = KotlinBindingExpressionWriter.Parse(assignment.BindingPath, assignment), Location = Location(assignment.LineNumberInfo)
                        });
                    connection.Bindings = connection.Bindings.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
                    page.Connections.Add(connection);
                }
                if (page.Connections.Any(x => x.FieldName != null)) page.Features.Add("named-elements");
                if (page.Connections.Any(x => x.Events.Count != 0)) page.Features.Add("events");
                if (page.Connections.Any(x => x.Bindings.Count != 0)) page.Features.Add("compiled-bindings");
                if (page.Connections.Any(x => x.IsTemplateChild)) page.Features.Add("templates");
                if (page.Connections.Any(x => x.Phase != 0)) page.Features.Add("phased-bindings");
                if (page.Connections.Any(x => x.CanBeInstantiatedLater)) page.Features.Add("deferred-elements");
                page.Features.Sort(StringComparer.Ordinal);
                result.Pages.Add(page);
            }
            return result;
        }

        private static KotlinXamlBindingDeclaration Binding(BindAssignment assignment) => new KotlinXamlBindingDeclaration {
            Name = assignment.MemberName, DeclaringTypeName = assignment.MemberDeclaringType.UnderlyingType.FullName,
            TypeName = assignment.MemberType.UnderlyingType.FullName,
            Mode = assignment.IsTrackingTarget ? "TwoWay" : assignment.IsTrackingSource ? "OneWay" : "OneTime",
            IsAttachable = assignment.IsAttachable,
            IsLoad = assignment is BoundLoadAssignment, Phase = assignment.ComputedPhase,
            Expression = KotlinBindingExpressionWriter.Parse(assignment.BindingPath, assignment),
            BindBack = assignment.BindBackPath == null ? null : KotlinBindingExpressionWriter.Parse(assignment.BindBackPath, assignment),
            Converter = assignment.Converter, ConverterParameter = assignment.ConverterParameter, ConverterLanguage = assignment.ConverterLanguage,
            FallbackValue = LiteralOption(assignment, "FallbackValue"), TargetNullValue = LiteralOption(assignment, "TargetNullValue"),
            UpdateSourceTrigger = assignment.UpdateSourceTrigger.ToString(), Location = Location(assignment.LineNumberInfo)
        };

        private static KotlinXamlBindingExpression LiteralOption(BindAssignment assignment, string name)
        {
            var member = assignment.BindingNode.GetMemberNode(name);
            if (member == null) return null;
            var item = member.Item as XamlDomObject;
            if (item != null && item.Type.Name == "NullExtension")
                return new KotlinXamlBindingExpression { Kind = "literal", TypeName = "null" };
            var value = DomHelper.GetStringValueOfProperty(member);
            if (value == null) throw new ArgumentException($"Kotlin x:Bind ({assignment.LineNumber},{assignment.ColumnNumber}): {name} requires a literal.");
            return new KotlinXamlBindingExpression { Kind = "literal", TypeName = assignment.MemberType.UnderlyingType.FullName, Value = value };
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
