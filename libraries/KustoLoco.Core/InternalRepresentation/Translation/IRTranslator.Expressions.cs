//
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kusto.Language;
using Kusto.Language.Symbols;
using Kusto.Language.Syntax;
using Kusto.Language.Utils;
using KustoLoco.Core.Evaluation;
using KustoLoco.Core.Evaluation.BuiltIns;
using KustoLoco.Core.Evaluation.BuiltIns.Impl;
using KustoLoco.Core.Extensions;
using KustoLoco.Core.InternalRepresentation.Nodes.Expressions;
using KustoLoco.Core.InternalRepresentation.Nodes.Expressions.QueryOperators;
using KustoLoco.Core.InternalRepresentation.Nodes.Other;
using KustoLoco.Core.Util;

namespace KustoLoco.Core.InternalRepresentation;

internal partial class IRTranslator : DefaultSyntaxVisitor<IRNode>
{
    public override IRNode VisitBetweenExpression(BetweenExpression node)
    {
        var couple = node.Right.Accept(this)
            as IRExpressionCoupleNode;
        var signature = node.ReferencedSignature;

        var leftRange = couple!._left;
        var rightRange = couple._right;

        var parameterExpression = node.Left.Accept(this) as IRExpressionNode;

        //between used after datetime range thinks type is unknown...
        var irArguments = new[] { parameterExpression!, leftRange, rightRange };
        ApplyTypeCoercionsForBetween(irArguments);

        var overloadInfo = BuiltInOperators.GetOverload((OperatorSymbol)signature.Symbol,
            node.ResultType, irArguments);


        return new IRBuiltInScalarFunctionCallNode(signature,
            overloadInfo, new List<Parameter>(), IRListNode.From(irArguments), ScalarTypes.Bool);
    }

    /// <summary>
    /// For a "between" operator, we need to case all parameters to the type of thing we are considering
    /// which is expressed as the first argument 
    /// </summary>
    /// <param name="irArguments"></param>
    private static void ApplyTypeCoercionsForBetween(IRExpressionNode[] irArguments)

    {
        var desiredTargetType = irArguments[0].ResultType;
        //between has a "special" mode where you can supply a between (datetime, timespan) expression
        if (!desiredTargetType.IsNumeric())
            return;
        for (var i = 1; i < irArguments.Length; i++)
        {
            if (desiredTargetType.Simplify() != irArguments[i].ResultType.Simplify())
                irArguments[i] = new IRCastExpressionNode(irArguments[i], desiredTargetType);
        }
    }

    public IRNode VisitExpressionWithArray(Expression node, Expression left, ExpressionList right)
    {
        var signature = node.ReferencedSignature;
        var parameterExpression = left.Accept(this) as IRExpressionNode;
        var ja = new JsonArray();
        var expresions = right.Expressions
            .OfType<SeparatedElement>()
            .Select(e => e.Element)
            .OfType<LiteralExpression>()
            .ToArray();

        if (parameterExpression!.ResultType.IsStringOrArray())
        {
            var items = expresions
                .Select(expr => expr.LiteralValue as string)
                .ToArray();
            ja = JsonArrayHelper.From(items);
        }
        else if (parameterExpression.ResultType.IsNumeric())
        {
            var items = expresions
                .Select(expr => expr.LiteralValue as long?)
                .ToArray();
            ja = JsonArrayHelper.From(items);
        }
        else
        {
            throw new NotImplementedException("In expressions are only supported for arrays of string/long literals");
        }

        var str1 = new IRPreEvaluatedScalarExpressionNode(ja, DynamicArraySymbol.From("dynamic"));
        var irArguments = new[] { parameterExpression, str1 };
        var overloadInfo = BuiltInOperators.GetOverload((OperatorSymbol)signature.Symbol,
            node.ResultType, irArguments);
        return new IRBuiltInScalarFunctionCallNode(signature,
            overloadInfo, new List<Parameter>(), IRListNode.From(irArguments), ScalarTypes.Bool);
    }

    // Every element of the list takes part: a scalar, the elements of a dynamic array, the first column of a
    // tabular expression. https://learn.microsoft.com/kusto/query/in-operator
    public override IRNode VisitInExpression(InExpression node)
    {
        var op = node.Operator.Text.Replace(" ", "", StringComparison.Ordinal);
        var caseInsensitive = op.EndsWith('~');
        var negated = op.StartsWith('!');
        var left = (IRExpressionNode)node.Left.Accept(this);
        var elements = node.Right.Expressions
            .Select(e => (IRExpressionNode)e.Element.Accept(this))
            .ToList();
        return new IRInListNode(left, elements, InListDomainFor(node, caseInsensitive), caseInsensitive, negated);
    }

    /// <summary>The comparison domain: the left side's type; for a dynamic left side, the list's scalar type (ADX converts
    /// the dynamic value to it); always string for in~.</summary>
    private static InListDomain InListDomainFor(InExpression node, bool caseInsensitive)
    {
        if (caseInsensitive) return InListDomain.String;
        var left = node.Left.ResultType.Simplify();
        if (left is not DynamicSymbol && left != ScalarTypes.Dynamic) return DomainOf(left!) ?? InListDomain.String;

        var types = new List<InListDomain>();
        foreach (var element in node.Right.Expressions.Select(e => e.Element))
        {
            var type = element.ResultType;
            if (type is TableSymbol table)
            {
                if (table.Columns.Count > 0 && DomainOf(table.Columns[0].Type) is { } d) types.Add(d);
                continue;
            }
            if (type is DynamicSymbol || type == ScalarTypes.Dynamic)
            {
                if (DynamicLiteralItems(element) is { } items) types.AddRange(items.Select(i => LiteralDomain(i)).OfType<InListDomain>());
                continue;
            }
            if (DomainOf(type) is { } scalar) types.Add(scalar);
        }
        if (types.Count == 0) return InListDomain.String;
        if (types.TrueForAll(t => t == types[0])) return types[0];
        if (types.TrueForAll(t => t is InListDomain.Long or InListDomain.Real)) return InListDomain.Real;
        throw new NotSupportedException("a dynamic left side of 'in' against a list of mixed scalar types");
    }

    private static InListDomain? DomainOf(TypeSymbol type)
    {
        type = type.Simplify()!;
        if (type == ScalarTypes.String) return InListDomain.String;
        if (type == ScalarTypes.Long || type == ScalarTypes.Int) return InListDomain.Long;
        if (type == ScalarTypes.Real || type == ScalarTypes.Decimal) return InListDomain.Real;
        if (type == ScalarTypes.Bool) return InListDomain.Bool;
        if (type == ScalarTypes.DateTime) return InListDomain.DateTime;
        if (type == ScalarTypes.TimeSpan) return InListDomain.TimeSpan;
        if (type == ScalarTypes.Guid) return InListDomain.Guid;
        return null;
    }

    private static InListDomain? LiteralDomain(object? value) => value switch
    {
        string => InListDomain.String,
        long or int => InListDomain.Long,
        double or decimal => InListDomain.Real,
        bool => InListDomain.Bool,
        _ => null,
    };

    /// <summary>The literal values of a <c>dynamic([...])</c> array, inline or let-bound; null when it is not one.</summary>
    private static List<object?>? DynamicLiteralItems(Expression e, int depth = 0)
    {
        while (e is ParenthesizedExpression p) e = p.Expression;
        if (depth < 8 && e is NameReference { ReferencedSymbol: VariableSymbol { Source: Expression source } })
            return DynamicLiteralItems(source, depth + 1);
        if (e is not DynamicExpression { Expression: JsonArrayExpression array }) return null;
        return array.Values.Select(v => v.Element is LiteralExpression l ? l.LiteralValue : null).ToList();
    }

    /// <summary>The constant needles of a has_any / has_all list: string and numeric literals, let-bound constants and the
    /// items of literal dynamic arrays (inline or let-bound). Null when an element is not constant.</summary>
    private static List<string?>? ConstantNeedles(ExpressionList list)
    {
        var needles = new List<string?>();
        foreach (var element in list.Expressions.Select(e => e.Element))
        {
            if (ConstantScalar(element) is { } c) { needles.Add(c); continue; }
            if (DynamicLiteralItems(element) is { } items)
            {
                foreach (var item in items)
                {
                    if (NeedleText(item) is not { } t) return null;
                    needles.Add(t);
                }
                continue;
            }
            return null;
        }
        return needles;
    }

    private static string? ConstantScalar(Expression e, int depth = 0)
    {
        while (e is ParenthesizedExpression p) e = p.Expression;
        if (e is LiteralExpression l) return NeedleText(l.LiteralValue);
        if (depth < 8 && e is NameReference { ReferencedSymbol: VariableSymbol v } && v.Type is not DynamicSymbol && v.Type != ScalarTypes.Dynamic)
        {
            if (v.IsConstant && NeedleText(v.ConstantValue) is { } c) return c;
            if (v.Source is Expression source) return ConstantScalar(source, depth + 1);
        }
        return null;
    }

    private static string? NeedleText(object? value) => value switch
    {
        string s => s,
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => null,
    };

    // has_any / has_all take let-bound constants and literal dynamic arrays (expanded to their items); a list
    // that is not constant is refused instead of silently dropping its elements.
    // https://learn.microsoft.com/kusto/query/has-anyoperator , https://learn.microsoft.com/kusto/query/has-all-operator
    public override IRNode VisitHasAnyExpression(HasAnyExpression node) => VisitHasList(node, node.Left, node.Right, "has_any");

    public override IRNode VisitHasAllExpression(HasAllExpression node) => VisitHasList(node, node.Left, node.Right, "has_all");

    private IRNode VisitHasList(Expression node, Expression left, ExpressionList right, string name)
    {
        var needles = ConstantNeedles(right)
                      ?? throw new NotSupportedException($"{name} over a computed or tabular list is not supported");
        var signature = node.ReferencedSignature;
        var parameterExpression = (IRExpressionNode)left.Accept(this);
        var str1 = new IRPreEvaluatedScalarExpressionNode(JsonArrayHelper.From(needles.ToArray()), DynamicArraySymbol.From("dynamic"));
        var irArguments = new[] { parameterExpression, str1 };
        var overloadInfo = BuiltInOperators.GetOverload((OperatorSymbol)signature.Symbol, node.ResultType, irArguments);
        return new IRBuiltInScalarFunctionCallNode(signature, overloadInfo, new List<Parameter>(), IRListNode.From(irArguments),
            ScalarTypes.Bool);
    }

    public override IRNode VisitSimpleNamedExpression(SimpleNamedExpression node) => node.Expression.Accept(this);

    //TODO - this is a nightmare.... there's an assumption that elementary Kusto types are reference equatable but it simply
//isn't true (or else we can't trust it to be so).  Really we want to compare only the important properties.
    public override IRNode VisitNameReference(NameReference node)
    {
        if (_rowScope != TableSymbol.Empty)
        {
            var index = _rowScope.Members.IndexOf(node.ReferencedSymbol);
            if (index >= 0) return new IRRowScopeNameReferenceNode(node.ReferencedSymbol, node.ResultType, index);


            //try column lookup ...
            // dynamic values compare by kind: Kusto.Language may give the same column a shaped dynamic type
            var m = _rowScope.Members.FirstIndex(t =>
                t is ColumnSymbol cs && cs.Name == node.ReferencedSymbol.Name && cs.Type.Simplify() == node.ResultType.Simplify());
            if (m >= 0) return new IRRowScopeNameReferenceNode(node.ReferencedSymbol, node.ResultType, m);

            //if the node referenced symbol has type unknown because it may have come
            //from an expression that failed to be evaluated (possibly bug in parser) then 
            //try a more relaxed fit..

            if (node.ResultType == ScalarTypes.Unknown)
            {
                var matchingNames = _rowScope.Members.FirstIndex(t => t.Name == node.ReferencedSymbol.Name);

                if (matchingNames >= 0)
                    return new IRRowScopeNameReferenceNode(node.ReferencedSymbol, node.ResultType, matchingNames);
            }
        }

        // If we know more about this symbol, use this info.
        // This is useful when the NameReference we are visiting is a user-defined-function parameter,
        // in which case we need this additional information to ascertain if the data is e.g. scalar or columnar.
        // TODO: Do this right. Look-up by string from global scope is likely to produce wrong results in many cases.
        // Ideally we'd want to track argument to parameter variable symbols.
        return
            _inScopeSymbolInfos.TryGetValue(node.Name.SimpleName, out var resultKind)
                ? new IRNameReferenceNode(node.ReferencedSymbol, node.ResultType, resultKind)
                : new IRNameReferenceNode(node.ReferencedSymbol, node.ResultType);
    }

    public override IRNode VisitLiteralExpression(LiteralExpression node)
    {
        //There appears to be a bug in KustoLanguage where the LiteralValue is parsed using the current culture for the real type
        if (node.ResultType == ScalarTypes.Real)
            return new IRLiteralExpressionNode(double.TryParse(node.LiteralValueInfo.ValueText,CultureInfo.InvariantCulture,out var f ) ?f :null, node.ResultType);

        if (node.ResultType == ScalarTypes.Decimal)
            return new IRLiteralExpressionNode(decimal.TryParse(node.LiteralValueInfo.ValueText, CultureInfo.InvariantCulture, out var f) ? f : null, node.ResultType);

        return new IRLiteralExpressionNode(node.LiteralValue, node.ResultType);
    }

    public override IRNode VisitDynamicExpression(DynamicExpression node)
    {
        var literalValue = (string)node.LiteralValue;
        try
        {
            // strings that read as a datetime or a guid are typed, as ADX does
            var parsedValue = DynamicStringTyping.Apply(JsonNode.Parse(literalValue));
            return new IRLiteralExpressionNode(parsedValue, node.ResultType);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"A literal dynamic expression was provided that is valid in Kusto but doesn't conform to the stricter JSON parser used by KustoLoco. Please rewrite the literal expression to be properly formatted JSON (invalid input: \"{literalValue}\")");
        }
    }

    public override IRNode VisitPathExpression(PathExpression node)
    {
        var irExpression = (IRExpressionNode)node.Expression.Accept(this);

        return node.Selector is NameReference selectorName
            ? (IRNode)new IRMemberAccessNode(irExpression, selectorName.SimpleName, node.ResultType)
            : throw new InvalidOperationException(
                $"Expected path selector to be {TypeNameHelper.GetTypeDisplayName(typeof(NameReference))}, but found {TypeNameHelper.GetTypeDisplayName(node.Selector)}");
    }

    public override IRNode VisitElementExpression(ElementExpression node)
    {
        var irExpression = (IRExpressionNode)node.Expression.Accept(this);

        if (node.Selector is not BracketedExpression selector)
            throw new InvalidOperationException(
                $"Expected element selector to be {TypeNameHelper.GetTypeDisplayName(typeof(NameReference))}, but found {TypeNameHelper.GetTypeDisplayName(node.Selector)}");

        var selectorExpression = selector.Expression;

        if (selectorExpression is PrefixUnaryExpression pfxExpression)
        {
            //TODO HERE - this is a horrible hack to allow for negative array indices
            //surely there has to be a better way....
            var pvalue = pfxExpression.Accept(this) as IRUnaryExpressionNode;
            if (pvalue!.Expression is IRLiteralExpressionNode lit)
            {
                var v = (long)lit.Value!;
                var op = pvalue.Signature.Symbol.Name;
                if (op == Operators.UnaryMinus.Name) v = -v;
                return new IRArrayAccessNode(irExpression, (int)v, node.ResultType);
            }
        }

        if (selectorExpression is not LiteralExpression literalExpressionSelector)
            throw new InvalidOperationException(
                $"Expected element selector expression to be {TypeNameHelper.GetTypeDisplayName(typeof(LiteralExpression))}, but found {TypeNameHelper.GetTypeDisplayName(selector.Expression)}");

        var value = selectorExpression.Accept(this) as IRLiteralExpressionNode;
        if (value == null)
            throw new InvalidOperationException(
                $"Expected element selector expression to evaluate to {TypeNameHelper.GetTypeDisplayName(typeof(IRLiteralExpressionNode))}, but found {TypeNameHelper.GetTypeDisplayName(value)}");


        //support array access for dynamic objects
        if (value.Value is long index) return new IRArrayAccessNode(irExpression, (int)index, node.ResultType);


        return value.Value is not string stringValue
            ? throw new InvalidOperationException(
                $"Element selector expression evaluated to null or to an unexpected data type ({TypeNameHelper.GetTypeDisplayName(value.Value)})")
            : new IRMemberAccessNode(irExpression, stringValue, node.ResultType);
    }

    public override IRNode VisitCompoundStringLiteralExpression(CompoundStringLiteralExpression node) =>
        new IRLiteralExpressionNode(node.LiteralValue, node.ResultType);

    public override IRNode VisitPipeExpression(PipeExpression node)
    {
        var irExpression = (IRExpressionNode)node.Expression.Accept(this);
        var oldRowScope = _rowScope;
        _rowScope = (TableSymbol)irExpression.ResultType;
        try
        {
            var irOperator = (IRQueryOperatorNode)node.Operator.Accept(this);

            return new IRPipeExpressionNode(irExpression, irOperator, node.ResultType);
        }
        finally
        {
            _rowScope = oldRowScope;
        }
    }

    public override IRNode VisitBinaryExpression(BinaryExpression node)
    {
        var signature = node.ReferencedSignature;

        var irLeft = (IRExpressionNode)node.Left.Accept(this);
        var irRight = (IRExpressionNode)node.Right.Accept(this);

        var irArguments = new[] { irLeft, irRight };
        if (ComparisonKinds.Contains(node.Kind))
            ConvertDynamicOperandToTypedOperand(irArguments);
        var overloadInfo = BuiltInOperators.GetOverload((OperatorSymbol)signature.Symbol,
            node.ResultType, irArguments);

        ApplyTypeCoercions(irArguments, overloadInfo);
        return new IRBinaryExpressionNode(signature, overloadInfo, irArguments[0], irArguments[1], node.ResultType);
    }

    private static readonly HashSet<SyntaxKind> ComparisonKinds =
    [
        SyntaxKind.EqualExpression, SyntaxKind.NotEqualExpression,
        SyntaxKind.LessThanExpression, SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.GreaterThanExpression, SyntaxKind.GreaterThanOrEqualExpression,
    ];

    // The conversion ADX applies to a dynamic value compared with a scalar of each type.
    private static readonly Dictionary<TypeSymbol, FunctionSymbol> DynamicComparisonConversions = new()
    {
        [ScalarTypes.Int] = Functions.ToInt,
        [ScalarTypes.Long] = Functions.ToLong,
        [ScalarTypes.Real] = Functions.ToReal,
        [ScalarTypes.Decimal] = Functions.ToDecimal,
        [ScalarTypes.Bool] = Functions.ToBool,
        [ScalarTypes.DateTime] = Functions.ToDateTime,
        [ScalarTypes.TimeSpan] = Functions.ToTimespan,
        [ScalarTypes.Guid] = Functions.ToGuid,
    };

    /// <summary>
    ///     ADX compares a dynamic value with a typed scalar (a number, bool, datetime, timespan or guid) by converting
    ///     the dynamic value to the scalar's type, as the matching to-function does: dynamic("0123") == 123 and
    ///     dynamic("2015-01-01T10:20:30Z") == datetime(2015-01-01T10:20:30Z) are true, and dynamic("123") > 100 is
    ///     an ordered comparison of longs. A comparison with a string (or with another dynamic) is left as it is,
    ///     because ADX compares the text there.
    /// </summary>
    private void ConvertDynamicOperandToTypedOperand(IRExpressionNode[] irArguments)
    {
        for (var i = 0; i < irArguments.Length; i++)
        {
            if (irArguments[i].ResultType.Simplify() != ScalarTypes.Dynamic) continue;
            var other = irArguments[1 - i].ResultType.Simplify();
            if (!DynamicComparisonConversions.TryGetValue(other, out var conversion)) return;
            var signature = conversion.Signatures[0];
            var parameters = signature.Parameters.Take(1).ToList();
            var arguments = new[] { irArguments[i] };
            if (!FunctionFinder.TryGetOverload(_functions, conversion, other, arguments, parameters, out var overload))
                return;
            ApplyTypeCoercions(arguments, overload!);
            irArguments[i] = new IRBuiltInScalarFunctionCallNode(signature, overload!, parameters,
                IRListNode.From(arguments), other);
            return;
        }
    }

    public override IRNode VisitExpressionCouple(ExpressionCouple node)
    {
        var irLeft = (IRExpressionNode)node.First.Accept(this);
        var irRight = (IRExpressionNode)node.Second.Accept(this);

        return new IRExpressionCoupleNode(irLeft, irRight);
    }

    public override IRNode VisitPrefixUnaryExpression(PrefixUnaryExpression node)
    {
        var signature = node.ReferencedSignature;

        var irExpression = (IRExpressionNode)node.Expression.Accept(this);


        var irArguments = new[] { irExpression };
        var overloadInfo = BuiltInOperators.GetOverload((OperatorSymbol)signature.Symbol, node.ResultType, irArguments);

        ApplyTypeCoercions(irArguments, overloadInfo);
        return new IRUnaryExpressionNode(signature, overloadInfo, irExpression, node.ResultType);
    }

    public override IRNode VisitFunctionCallExpression(FunctionCallExpression node)
    {
        var signature = node.ReferencedSignature;
        var returnType = node.ResultType;

        var arguments = new Expression[node.ArgumentList.Expressions.Count];
        var irArguments = new IRExpressionNode[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = node.ArgumentList.Expressions[i].Element;
            arguments[i] = argument;
            irArguments[i] = (IRExpressionNode)argument.Accept(this);
        }

        var parameters = signature.GetArgumentParameters(arguments);

        var isUserFunction = signature.Declaration != null; // user functions have declarations
        if (isUserFunction)
        {
            var expansion = node.GetCalledFunctionBody();
            if (expansion == null)
                throw new InvalidOperationException(
                    $"Failed to get function expansion for {node.Name.SimpleName}.");

            var paramSymbols = new VariableSymbol[arguments.Length];
            for (var i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].DeclaredTypes.Count != 1)
                    throw new InvalidOperationException(
                        $"Parameters with more than one declared type is not supported, found {parameters[i].DeclaredTypes.Count}.");

                paramSymbols[i] = new VariableSymbol(parameters[i].Name, parameters[i].DeclaredTypes.Single());
            }

            IRFunctionBodyNode irFunctionBody;
            {
                var nestedTranslator = new IRTranslator(_functions);
                for (var i = 0; i < arguments.Length; i++)
                {
                    // NOTE: For now we only support type coercions for scalars. Bad things may happen for tabular inputs, oh well...
                    if (irArguments[i].ResultType is ScalarSymbol &&
                        paramSymbols[i].Type.Simplify() != irArguments[i].ResultType.Simplify())
                        irArguments[i] = new IRCastExpressionNode(irArguments[i], paramSymbols[i].Type);

                    nestedTranslator.SetInScopeSymbolInfo(paramSymbols[i].Name, irArguments[i].ResultKind);
                }

                irFunctionBody = (IRFunctionBodyNode)expansion.Accept(nestedTranslator);
            }

            return new IRUserFunctionCallNode(signature, parameters, paramSymbols, irFunctionBody,
                IRListNode.From(irArguments), node.ResultType);
        }

        var functionSymbol = (FunctionSymbol)signature.Symbol;
        if (functionSymbol == Functions.PackAll)
        {
            irArguments = ExpandPackAllArguments(irArguments);
            parameters = BuildPackAllParameters(irArguments.Length);
        }

        if (FunctionFinder.TryGetOverload(_functions, functionSymbol, returnType, irArguments, parameters,
                out var functionOverload))
        {
            ApplyTypeCoercions(irArguments, functionOverload!);
            return new IRBuiltInScalarFunctionCallNode(signature, functionOverload!, parameters,
                IRListNode.From(irArguments), node.ResultType);
        }

        //try the int->long return type conversion for aggregate types 
        var types = returnType == ScalarTypes.Int
            ? new[] { ScalarTypes.Int, ScalarTypes.Long }
            : new[] { returnType };
        foreach (var expectedReturnType in types)
            if (BuiltInAggregates.TryGetOverload(functionSymbol, expectedReturnType, irArguments, parameters,
                    out var aggregateOverload))
            {
                irArguments = irArguments.Take(aggregateOverload!.NumParametersToMatch).ToArray();
                parameters = parameters.Take(aggregateOverload.NumParametersToMatch).ToList();
                ApplyTypeCoercions(irArguments, aggregateOverload);
                return new IRAggregateCallNode(signature, aggregateOverload, parameters, IRListNode.From(irArguments),
                    expectedReturnType);
            }

        if (BuiltInWindowFunctions.TryGetOverload(functionSymbol, returnType, irArguments, parameters,
                out var windowFunctionOverload))
        {
            ApplyTypeCoercions(irArguments, windowFunctionOverload!);
            return new IRBuiltInWindowFunctionCallNode(signature, windowFunctionOverload!, parameters,
                IRListNode.From(irArguments), node.ResultType);
        }

        throw new InvalidOperationException(
            $"Function {functionSymbol.Name}{SchemaDisplay.GetText(functionSymbol)} is not implemented for arguments {string.Join(", ", arguments.Select(arg => SchemaDisplay.GetText(arg.ResultType)))}");
    }

    private static void ApplyTypeCoercions(IRExpressionNode[] irArguments, OverloadInfoBase overloadInfo)

    {
        var loop = overloadInfo.RepeatParams;
        for (var i = 0; i < irArguments.Length; i++)
        {
            var p = loop ? 0 : i;
            if (overloadInfo.ParameterTypes[p].Simplify() != irArguments[i].ResultType.Simplify())
                //ideally we would never need to do type coercions since we'd generate
                //all possible overload variants
                irArguments[i] = new IRCastExpressionNode(irArguments[i], overloadInfo.ParameterTypes[p]);
        }
    }

    public override IRNode VisitMaterializeExpression(MaterializeExpression node)
    {
        var irExpression = (IRExpressionNode)node.Expression.Accept(this);
        return new IRMaterializeExpressionNode(irExpression, node.ResultType);
    }

    public override IRNode VisitParenthesizedExpression(ParenthesizedExpression node) => node.Expression.Accept(this);

    public override IRNode VisitDataTableExpression(DataTableExpression node)
    {
        var numColumns = node.Schema.Columns.Count;

        if (node.Values.Count % numColumns != 0)
            throw new InvalidOperationException(
                $"Expected number of values ({node.Values.Count}) to be a multiple of the number of columns ({numColumns}).");

        var data = new object?[node.Values.Count];
        for (var i = 0; i < node.Values.Count; i++)
        {
            var irLiteralExpression = node.Values[i].Element.Accept(this);
            if (irLiteralExpression is not IRLiteralExpressionNode literalExpression)
                throw new InvalidOperationException(
                    $"Expected literal expression in datatable values, found {TypeNameHelper.GetTypeDisplayName(irLiteralExpression)}.");

            data[i] = literalExpression.Value;
        }

        return new IRDataTableExpression(data, node.ResultType);
    }

    public override IRNode VisitToScalarExpression(ToScalarExpression node)
    {
        var irExpression = (IRExpressionNode)node.Expression.Accept(this);
        return new IRToScalarExpressionNode(irExpression, node.ResultType);
    }

    // pack_all's source signature is (ignore_null_empty?: bool); the IR expansion appends
    // one (name, value) pair per row-scope column so a registered overload can resolve it.
    private IRExpressionNode[] ExpandPackAllArguments(IRExpressionNode[] irArguments)
    {
        var flag = irArguments.Length == 0
            ? new IRLiteralExpressionNode(false, ScalarTypes.Bool)
            : irArguments[0];

        var expanded = new List<IRExpressionNode>(1 + _rowScope.Members.Count * 2) { flag };
        for (var i = 0; i < _rowScope.Members.Count; i++)
        {
            if (_rowScope.Members[i] is not ColumnSymbol column) continue;
            expanded.Add(new IRLiteralExpressionNode(column.Name, ScalarTypes.String));
            expanded.Add(new IRRowScopeNameReferenceNode(column, column.Type, i));
        }
        return expanded.ToArray();
    }

    private static List<Parameter> BuildPackAllParameters(int argumentCount)
    {
        var parameters = new List<Parameter>(argumentCount) { new("ignore_null_empty", ScalarTypes.Bool) };
        for (var i = 1; i + 1 < argumentCount; i += 2)
        {
            parameters.Add(new Parameter("key", ScalarTypes.String));
            parameters.Add(new Parameter("value", ScalarTypes.Unknown));
        }
        return parameters;
    }
}
