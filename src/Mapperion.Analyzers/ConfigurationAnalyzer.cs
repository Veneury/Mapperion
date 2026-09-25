using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Mapperion.Analysis
{
    /// <summary>
    /// Reports the configuration mistakes that can be seen without running anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What it does not attempt: deciding whether a destination member will find a source. That is
    /// what <c>AssertIsValid</c> is for, and answering it here would mean a second copy of the
    /// convention engine living beside the first one and drifting from it. The checks here are the
    /// ones the written configuration answers on its own.
    /// </para>
    /// <para>
    /// It reads fluent chains, which is how these are written. A configuration split across
    /// statements, holding the expression in a local and calling into it later, is not followed,
    /// and nothing is reported about it rather than something wrong.
    /// </para>
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ConfigurationAnalyzer : DiagnosticAnalyzer
    {
        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(
                ConfigurationDiagnostics.DuplicateMap,
                ConfigurationDiagnostics.SourcedTwice,
                ConfigurationDiagnostics.IgnoredAndSourced,
                ConfigurationDiagnostics.ConstructorConfiguredTwoWays);

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(start =>
            {
                KnownTypes? known = KnownTypes.From(start.Compilation);

                if (known is null)
                {
                    return;
                }

                start.RegisterSyntaxNodeAction(c => Chain(c, known), SyntaxKind.InvocationExpression);

                start.RegisterSyntaxNodeAction(
                    c => Scope(c, known),
                    SyntaxKind.SimpleLambdaExpression,
                    SyntaxKind.ParenthesizedLambdaExpression,
                    SyntaxKind.ConstructorDeclaration);
            });
        }

        /// <summary>
        /// One configuration's worth of <c>CreateMap</c> calls: the callback handed to
        /// <c>MapperConfiguration</c>, or the constructor of a profile.
        /// </summary>
        private static void Scope(SyntaxNodeAnalysisContext context, KnownTypes known)
        {
            if (!IsScope(context.Node, context.SemanticModel, known, context.CancellationToken))
            {
                return;
            }

            var declared = new Dictionary<string, Location>();

            foreach (InvocationExpressionSyntax invocation in
                context.Node.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                        is not IMethodSymbol method ||
                    method.Name != "CreateMap" ||
                    !known.IsConfiguration(method.ContainingType) ||
                    method.TypeArguments.Length != 2)
                {
                    continue;
                }

                // A configuration nested inside this one owns its own declarations.
                if (Enclosing(invocation, context.SemanticModel, known, context.CancellationToken) != context.Node)
                {
                    continue;
                }

                string pair = Key(method.TypeArguments[0], method.TypeArguments[1]);

                if (declared.TryGetValue(pair, out Location first))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ConfigurationDiagnostics.DuplicateMap,
                        invocation.GetLocation(),
                        new[] { first },
                        method.TypeArguments[0].Name,
                        method.TypeArguments[1].Name));

                    continue;
                }

                declared.Add(pair, invocation.GetLocation());
            }
        }

        /// <summary>
        /// One fluent chain rooted at <c>CreateMap</c>, cut into segments at every
        /// <c>ReverseMap</c>, since what follows one of those configures the other direction.
        /// </summary>
        private static void Chain(SyntaxNodeAnalysisContext context, KnownTypes known)
        {
            var outermost = (InvocationExpressionSyntax)context.Node;

            if (outermost.Parent is MemberAccessExpressionSyntax access &&
                access.Parent is InvocationExpressionSyntax)
            {
                return;
            }

            List<Call> calls = Calls(outermost, context.SemanticModel, known, context.CancellationToken);

            if (calls.Count == 0 || calls[0].Name != "CreateMap")
            {
                return;
            }

            var members = new Dictionary<string, Settings>();
            bool constructUsing = false;
            bool forCtorParam = false;

            foreach (Call call in calls)
            {
                switch (call.Name)
                {
                    case "ReverseMap":
                        Report(context, members);
                        members.Clear();
                        constructUsing = false;
                        forCtorParam = false;
                        continue;

                    case "ConstructUsing":
                        constructUsing = true;
                        break;

                    case "ForCtorParam":
                        forCtorParam = true;
                        break;

                    case "ForMember":
                    case "ForPath":
                        Member(context, known, call, members);
                        break;
                }

                if (constructUsing && forCtorParam)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ConfigurationDiagnostics.ConstructorConfiguredTwoWays,
                        call.Invocation.GetLocation()));

                    constructUsing = false;
                    forCtorParam = false;
                }
            }

            Report(context, members);
        }

        /// <summary>
        /// What one segment of the chain ended up saying about each member it named.
        /// </summary>
        private static void Report(SyntaxNodeAnalysisContext context, Dictionary<string, Settings> members)
        {
            foreach (KeyValuePair<string, Settings> entry in members)
            {
                if (entry.Value.Sources > 1)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ConfigurationDiagnostics.SourcedTwice,
                        entry.Value.Last,
                        new[] { entry.Value.First },
                        entry.Key));
                }

                if (entry.Value.Ignored && entry.Value.Sources > 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        ConfigurationDiagnostics.IgnoredAndSourced,
                        entry.Value.Last,
                        new[] { entry.Value.First },
                        entry.Key));
                }
            }
        }

        /// <summary>
        /// Adds what one <c>ForMember</c> or <c>ForPath</c> says about its member to what the
        /// earlier ones said, since the two can disagree across calls as easily as within one.
        /// </summary>
        private static void Member(
            SyntaxNodeAnalysisContext context,
            KnownTypes known,
            Call call,
            Dictionary<string, Settings> members)
        {
            if (call.Invocation.ArgumentList.Arguments.Count < 2)
            {
                return;
            }

            string? name = Path(call.Invocation.ArgumentList.Arguments[0].Expression);

            if (name is null)
            {
                return;
            }

            int sources = 0;
            bool ignored = false;

            foreach (InvocationExpressionSyntax invocation in call.Invocation.ArgumentList.Arguments[1]
                .Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            {
                if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                        is not IMethodSymbol method ||
                    !known.IsMemberOptions(method.ContainingType))
                {
                    continue;
                }

                if (method.Name == "MapFrom")
                {
                    sources++;
                }

                ignored |= method.Name == "Ignore";
            }

            Location here = call.Invocation.GetLocation();

            members[name] = members.TryGetValue(name, out Settings already)
                ? new Settings(already.Sources + sources, already.Ignored || ignored, already.First, here)
                : new Settings(sources, ignored, here, here);
        }

        /// <summary>
        /// The calls of a fluent chain, innermost first, or nothing when the chain is not one of
        /// ours all the way down.
        /// </summary>
        private static List<Call> Calls(
            InvocationExpressionSyntax outermost,
            SemanticModel model,
            KnownTypes known,
            CancellationToken cancellation)
        {
            var calls = new List<Call>();

            for (ExpressionSyntax? current = outermost; current is InvocationExpressionSyntax invocation;)
            {
                if (model.GetSymbolInfo(invocation, cancellation).Symbol is not IMethodSymbol method ||
                    (!known.IsMapping(method.ContainingType) && !known.IsConfiguration(method.ContainingType)))
                {
                    return new List<Call>();
                }

                calls.Add(new Call(method.Name, invocation));

                current = invocation.Expression is MemberAccessExpressionSyntax access ? access.Expression : null;
            }

            calls.Reverse();
            return calls;
        }

        private static bool IsScope(
            SyntaxNode node,
            SemanticModel model,
            KnownTypes known,
            CancellationToken cancellation)
        {
            if (node is ConstructorDeclarationSyntax constructor)
            {
                return known.IsProfile(model.GetDeclaredSymbol(constructor, cancellation)?.ContainingType);
            }

            return node is LambdaExpressionSyntax lambda &&
                model.GetSymbolInfo(lambda, cancellation).Symbol is IMethodSymbol method &&
                method.Parameters.Length == 1 &&
                known.IsConfiguration(method.Parameters[0].Type);
        }

        /// <summary>
        /// The configuration scope a call belongs to, which is the innermost one around it.
        /// </summary>
        private static SyntaxNode? Enclosing(
            SyntaxNode node,
            SemanticModel model,
            KnownTypes known,
            CancellationToken cancellation)
        {
            for (SyntaxNode? current = node.Parent; current is not null; current = current.Parent)
            {
                if (IsScope(current, model, known, cancellation))
                {
                    return current;
                }
            }

            return null;
        }

        /// <summary>
        /// The destination member a lambda points at, as it is written, so that
        /// <c>d =&gt; d.Customer.Name</c> becomes <c>Customer.Name</c>.
        /// </summary>
        private static string? Path(ExpressionSyntax expression)
        {
            if (expression is not LambdaExpressionSyntax lambda || lambda.Body is not ExpressionSyntax body)
            {
                return null;
            }

            var steps = new List<string>();

            for (ExpressionSyntax? current = body; current is MemberAccessExpressionSyntax access;)
            {
                steps.Add(access.Name.Identifier.ValueText);
                current = access.Expression;
            }

            if (steps.Count == 0)
            {
                return null;
            }

            steps.Reverse();
            return string.Join(".", steps);
        }

        private static string Key(ITypeSymbol source, ITypeSymbol destination) =>
            source.ToDisplayString() + " -> " + destination.ToDisplayString();

        private readonly struct Settings
        {
            internal Settings(int sources, bool ignored, Location first, Location last)
            {
                Sources = sources;
                Ignored = ignored;
                First = first;
                Last = last;
            }

            internal int Sources { get; }

            internal bool Ignored { get; }

            internal Location First { get; }

            internal Location Last { get; }
        }

        private readonly struct Call
        {
            internal Call(string name, InvocationExpressionSyntax invocation)
            {
                Name = name;
                Invocation = invocation;
            }

            internal string Name { get; }

            internal InvocationExpressionSyntax Invocation { get; }
        }
    }
}
