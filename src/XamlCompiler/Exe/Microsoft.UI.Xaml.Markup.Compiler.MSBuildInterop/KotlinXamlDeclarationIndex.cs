// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System.Collections.Generic;

namespace Microsoft.UI.Xaml.Markup.Compiler.MSBuildInterop
{
    public sealed class KotlinXamlImplementationPlan
    {
        public int SchemaVersion { get; set; } = 2;
        public string DeclarationFingerprint { get; set; }
        public KotlinXamlDeclarationIndex Declarations { get; set; }
    }
    public sealed class KotlinXamlSemanticSymbols
    {
        public int SchemaVersion { get; set; }
        public string DeclarationFingerprint { get; set; }
        public KotlinXamlDeclarationIndex Declarations { get; set; }
        public List<KotlinXamlPageSymbols> Pages { get; set; }
    }

    public sealed class KotlinXamlPageSymbols
    {
        public string ClassName { get; set; }
        public List<KotlinXamlHandlerSymbol> Handlers { get; set; }
    }

    public sealed class KotlinXamlHandlerSymbol
    {
        public string Name { get; set; }
        public string ReturnTypeName { get; set; }
        public List<string> ParameterTypeNames { get; set; }
    }

    // Language-neutral WinRT names are intentional. Kotlin projection mapping belongs to kotlin-winrt.
    public sealed class KotlinXamlDeclarationIndex
    {
        public int SchemaVersion { get; set; } = 2;
        public List<KotlinXamlPageDeclaration> Pages { get; set; } = new List<KotlinXamlPageDeclaration>();
        public List<string> Resources { get; set; } = new List<string>();
    }

    public sealed class KotlinXamlPageDeclaration
    {
        public string ClassName { get; set; }
        public string ResourcePath { get; set; }
        public string BaseTypeName { get; set; }
        public bool IsApplication { get; set; }
        public List<string> Features { get; set; } = new List<string>();
        public List<KotlinXamlConnectionDeclaration> Connections { get; set; } = new List<KotlinXamlConnectionDeclaration>();
    }

    public sealed class KotlinXamlConnectionDeclaration
    {
        public int Id { get; set; }
        public string TypeName { get; set; }
        public string FieldName { get; set; }
        public string ElementName { get; set; }
        public int ScopeId { get; set; }
        public bool IsScopeRoot { get; set; }
        public bool IsTemplateChild { get; set; }
        public string DataTypeName { get; set; }
        public int Phase { get; set; }
        public bool CanBeInstantiatedLater { get; set; }
        public bool IsUnloadableRoot { get; set; }
        public List<int> Children { get; set; } = new List<int>();
        public KotlinXamlSourceLocation Location { get; set; }
        public List<KotlinXamlEventDeclaration> Events { get; set; } = new List<KotlinXamlEventDeclaration>();
        public List<KotlinXamlBindingDeclaration> Bindings { get; set; } = new List<KotlinXamlBindingDeclaration>();
    }

    public sealed class KotlinXamlBindingDeclaration
    {
        public string Name { get; set; }
        public string DeclaringTypeName { get; set; }
        public string TypeName { get; set; }
        public string Mode { get; set; }
        public bool IsAttachable { get; set; }
        public bool IsEvent { get; set; }
        public bool IsLoad { get; set; }
        public int Phase { get; set; }
        public KotlinXamlBindingExpression Expression { get; set; }
        public KotlinXamlBindingExpression BindBack { get; set; }
        public string Converter { get; set; }
        public string ConverterParameter { get; set; }
        public string ConverterLanguage { get; set; }
        public KotlinXamlBindingExpression FallbackValue { get; set; }
        public KotlinXamlBindingExpression TargetNullValue { get; set; }
        public string UpdateSourceTrigger { get; set; }
        public KotlinXamlSourceLocation Location { get; set; }
    }

    // Syntax comes from the existing BindingPath ANTLR parser. Kotlin IR owns
    // member resolution, including private members and non-WinRT view models.
    public sealed class KotlinXamlBindingExpression
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string Value { get; set; }
        public KotlinXamlBindingExpression Receiver { get; set; }
        public List<KotlinXamlBindingExpression> Arguments { get; set; } = new List<KotlinXamlBindingExpression>();
    }

    public sealed class KotlinXamlEventDeclaration
    {
        public string Name { get; set; }
        public string HandlerName { get; set; }
        public string DeclaringTypeName { get; set; }
        public string DelegateTypeName { get; set; }
        public KotlinXamlSourceLocation Location { get; set; }
    }

    public sealed class KotlinXamlSourceLocation
    {
        public int Line { get; set; }
        public int Column { get; set; }
    }
}
