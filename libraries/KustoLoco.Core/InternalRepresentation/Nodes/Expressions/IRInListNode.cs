//
// Licensed under the MIT License.

// List membership with ADX semantics: https://learn.microsoft.com/kusto/query/in-operator

using System;
using System.Collections.Generic;
using Kusto.Language.Symbols;

namespace KustoLoco.Core.InternalRepresentation.Nodes.Expressions;

/// <summary>How the left side and the list elements are compared.</summary>
internal enum InListDomain { String, Long, Real, Bool, DateTime, TimeSpan, Guid }

/// <summary>
/// <c>left in (e1, e2, …)</c> (and <c>in~</c>, <c>!in</c>, <c>!in~</c>). Each element is evaluated: a scalar contributes
/// its value, a dynamic array contributes its ELEMENTS, a tabular expression contributes the values of its first column.
/// Values compare in <see cref="Domain"/>: the left side's scalar type, or, for a dynamic left side, the scalar type of the
/// list (ADX converts the dynamic value to it).
/// </summary>
internal sealed class IRInListNode : IRExpressionNode
{
    public IRInListNode(IRExpressionNode left, IReadOnlyList<IRExpressionNode> elements, InListDomain domain,
        bool caseInsensitive, bool negated)
        : base(ScalarTypes.Bool, KindOf(left, elements))
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Elements = elements;
        Domain = domain;
        CaseInsensitive = caseInsensitive;
        Negated = negated;
    }

    public IRExpressionNode Left { get; }
    public IReadOnlyList<IRExpressionNode> Elements { get; }
    public InListDomain Domain { get; }
    public bool CaseInsensitive { get; }
    public bool Negated { get; }

    public override int ChildCount => 1 + Elements.Count;

    public override IRNode GetChild(int index) =>
        index == 0 ? Left
        : index > 0 && index <= Elements.Count ? Elements[index - 1]
        : throw new ArgumentOutOfRangeException(nameof(index));

    public override TResult Accept<TResult, TContext>(IRNodeVisitor<TResult, TContext> visitor, TContext context) =>
        visitor.VisitInList(this, context);

    public override string ToString() => $"InList({(Negated ? "!" : "")}in{(CaseInsensitive ? "~" : "")}, {Domain})";

    private static EvaluatedExpressionKind KindOf(IRExpressionNode left, IReadOnlyList<IRExpressionNode> elements)
    {
        var kind = left.ResultKind;
        foreach (var e in elements)
            if (e.ResultKind == EvaluatedExpressionKind.Columnar)
                kind = EvaluatedExpressionKind.Columnar;
        return kind == EvaluatedExpressionKind.Table ? EvaluatedExpressionKind.Scalar : kind;
    }
}
