// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System.Collections.Generic;

namespace Microsoft.UI.Xaml.Markup.Compiler.MSBuildInterop
{
    public sealed class KotlinXamlImplementationPlan
    {
        public int SchemaVersion { get; set; } = 1;
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
        public int SchemaVersion { get; set; } = 1;
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
        public KotlinXamlSourceLocation Location { get; set; }
        public List<KotlinXamlEventDeclaration> Events { get; set; } = new List<KotlinXamlEventDeclaration>();
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
