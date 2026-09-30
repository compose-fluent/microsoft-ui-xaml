// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License. See LICENSE in the project root for license information.
using System;
using System.Linq;
using Antlr4.Runtime;
using Microsoft.UI.Xaml.Markup.Compiler.MSBuildInterop;

namespace Microsoft.UI.Xaml.Markup.Compiler
{
    internal static class KotlinBindingExpressionWriter
    {
        internal static KotlinXamlBindingExpression Parse(string path, BindAssignmentBase assignment)
        {
            if (string.IsNullOrWhiteSpace(path)) return Root();
            var lexer = new BindingPathLexer(new AntlrInputStream(path));
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new SyntaxErrors<int>(assignment));
            var parser = new BindingPathParser(new CommonTokenStream(lexer));
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new SyntaxErrors<IToken>(assignment));
            return Expression(parser.program().path(), assignment);
        }

        private static KotlinXamlBindingExpression Root() => new KotlinXamlBindingExpression { Kind = "root" };
        private static KotlinXamlBindingExpression Member(KotlinXamlBindingExpression receiver, string name) =>
            new KotlinXamlBindingExpression { Kind = "member", Receiver = receiver, Name = name };
        private static KotlinXamlBindingExpression Literal(string type, string value) =>
            new KotlinXamlBindingExpression { Kind = "literal", TypeName = type, Value = value };
        private static string Unquote(string text) => text.Substring(1, text.Length - 2).Replace("^\"", "\"").Replace("^'", "'");
        private static string ResolveType(string name, BindAssignmentBase assignment) =>
            assignment.ResolveXmlName(name)?.UnderlyingType?.FullName ??
                throw new ArgumentException($"Kotlin x:Bind ({assignment.LineNumber},{assignment.ColumnNumber}): unresolved type {name}.");
        private static KotlinXamlBindingExpression Static(string name, BindAssignmentBase assignment) =>
            new KotlinXamlBindingExpression { Kind = "static", TypeName = ResolveType(name, assignment) };
        private static KotlinXamlBindingExpression Cast(string name, KotlinXamlBindingExpression value, BindAssignmentBase assignment) =>
            new KotlinXamlBindingExpression { Kind = "cast", TypeName = ResolveType(name, assignment), Receiver = value };

        private static KotlinXamlBindingExpression Call(BindingPathParser.FunctionContext function,
            KotlinXamlBindingExpression receiver, BindAssignmentBase assignment) =>
            new KotlinXamlBindingExpression {
                Kind = "call", Name = function.IDENTIFIER().GetText(), Receiver = receiver,
                Arguments = function.function_param().Select(x => Parameter(x, assignment)).ToList()
            };

        private static KotlinXamlBindingExpression Expression(BindingPathParser.PathContext path, BindAssignmentBase assignment)
        {
            switch (path)
            {
                case BindingPathParser.PathIdentifierContext item:
                    return Member(Root(), item.IDENTIFIER().GetText());
                case BindingPathParser.PathDotIdentifierContext item:
                    return Member(Expression(item.path(), assignment), item.IDENTIFIER().GetText());
                case BindingPathParser.PathFunctionContext item:
                    return Call(item.function(), Root(), assignment);
                case BindingPathParser.PathPathToFunctionContext item:
                    return Call(item.function(), Expression(item.path(), assignment), assignment);
                case BindingPathParser.PathStaticIdentifierContext item:
                    return Member(Static(item.static_type().GetText(), assignment), item.IDENTIFIER().GetText());
                case BindingPathParser.PathStaticFuctionContext item:
                    return Call(item.function(), Static(item.static_type().GetText(), assignment), assignment);
                case BindingPathParser.PathIndexerContext item:
                    return new KotlinXamlBindingExpression { Kind = "index", Receiver = Expression(item.path(), assignment),
                        Arguments = { Literal("number", item.Digits().GetText()) } };
                case BindingPathParser.PathStringIndexerContext item:
                    return new KotlinXamlBindingExpression { Kind = "index", Receiver = Expression(item.path(), assignment),
                        Arguments = { Literal("System.String", Unquote(item.QuotedString().GetText())) } };
                case BindingPathParser.PathCastContext item:
                    return Cast(item.cast_expr().GetText().Trim('(', ')'), Root(), assignment);
                case BindingPathParser.PathCastPathContext item:
                    return Cast(item.cast_expr().GetText().Trim('(', ')'), Expression(item.path(), assignment), assignment);
                case BindingPathParser.PathCastPathParenContext item:
                    return Cast(item.cast_expr().GetText().Trim('(', ')'), Expression(item.path(), assignment), assignment);
                case BindingPathParser.PathDotAttachedContext item:
                    var expression = item.attached_expr().GetText().Trim('(', ')');
                    var separator = expression.LastIndexOf('.');
                    return new KotlinXamlBindingExpression { Kind = "attached", Name = expression.Substring(separator + 1),
                        TypeName = ResolveType(expression.Substring(0, separator), assignment), Receiver = Expression(item.path(), assignment) };
                default:
                    throw new ArgumentException($"Kotlin x:Bind ({assignment.LineNumber},{assignment.ColumnNumber}): invalid expression {path.GetText()}.");
            }
        }

        private static KotlinXamlBindingExpression Parameter(BindingPathParser.Function_paramContext parameter, BindAssignmentBase assignment)
        {
            switch (parameter)
            {
                case BindingPathParser.FunctionParamPathContext item: return Expression(item.path(), assignment);
                case BindingPathParser.FunctionParamBoolContext item: return Literal("System.Boolean", item.GetText() == "x:True" ? "true" : "false");
                case BindingPathParser.FunctionParamNumberContext item: return Literal("number", item.GetText());
                case BindingPathParser.FunctionParamStringContext item: return Literal("System.String", Unquote(item.GetText()));
                case BindingPathParser.FunctionParamNullValueContext _: return Literal("null", null);
                default: throw new ArgumentException($"Kotlin x:Bind ({assignment.LineNumber},{assignment.ColumnNumber}): invalid function argument.");
            }
        }

        private sealed class SyntaxErrors<T> : IAntlrErrorListener<T>
        {
            private readonly BindAssignmentBase assignment;
            internal SyntaxErrors(BindAssignmentBase assignment) { this.assignment = assignment; }
            public void SyntaxError(IRecognizer recognizer, T offendingSymbol, int line, int column, string message, RecognitionException exception) =>
                throw new ArgumentException($"Kotlin x:Bind ({assignment.LineNumber},{assignment.ColumnNumber}): {message}");
        }
    }
}
