// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Faqra contributors
// Mirrors the Parser section (:231-347) of Sources/Vorssaint/Services/CommandBar/CommandBarMath.swift.
// Split out of CommandBarMath.cs to keep both files under the project's ~400-line guideline.

namespace Faqra.Core.CommandBar;

public static partial class CommandBarMath
{
    /// <summary>A parsed value. <see cref="PercentRaw"/> remembers that the value was written as
    /// a percentage, which is what makes "480 + 15%" mean 552 instead of 480.15.</summary>
    private readonly record struct Operand(double Value, double? PercentRaw);

    /// <summary>Recursive-descent parser. Grammar, low to high precedence: Expression := Term
    /// (('+'|'-') Term)*; Term := Factor (('*'|'/'|of|'(') Factor)*; Factor := ('-'|'+') Factor |
    /// Power; Power := Primary ('^' Factor)?, right-associative; Primary := Number ['%'] |
    /// '(' Expression ')' ['%'].</summary>
    private sealed class Parser(List<Token> tokens)
    {
        private int index;
        /// <summary>Depth guard: a wall of "((((" must not recurse the stack away.</summary>
        private int depth;

        public bool IsAtEnd => index >= tokens.Count;

        public Operand? ParseExpression()
        {
            if (depth >= 32)
            {
                return null;
            }
            var left = ParseTerm();
            if (left is null)
            {
                return null;
            }
            var leftValue = left.Value;
            while (Peek() is { } token && (token.Kind == TokenKind.Plus || token.Kind == TokenKind.Minus))
            {
                Advance();
                var right = ParseTerm();
                if (right is null)
                {
                    return null;
                }
                // A percentage after + or - is relative to what came before.
                var delta = right.Value.PercentRaw is { } percent ? leftValue.Value * percent / 100 : right.Value.Value;
                leftValue = new Operand(token.Kind == TokenKind.Plus ? leftValue.Value + delta : leftValue.Value - delta, null);
            }
            return leftValue;
        }

        private Operand? ParseTerm()
        {
            var left = ParseFactor();
            if (left is null)
            {
                return null;
            }
            var leftValue = left.Value;
            while (Peek() is { } token)
            {
                // A number against a parenthesis multiplies, written or not.
                if (token.Kind == TokenKind.LeftParen)
                {
                    var implicitRight = ParseFactor();
                    if (implicitRight is null)
                    {
                        return null;
                    }
                    leftValue = new Operand(leftValue.Value * implicitRight.Value.Value, null);
                    continue;
                }
                if (token.Kind != TokenKind.Times && token.Kind != TokenKind.Divide && token.Kind != TokenKind.OfWord)
                {
                    break;
                }
                Advance();
                if (token.Kind == TokenKind.OfWord)
                {
                    // "20% of 480": only a percentage can own an "of".
                    if (leftValue.PercentRaw is null)
                    {
                        return null;
                    }
                    var ofRight = ParseFactor();
                    if (ofRight is null)
                    {
                        return null;
                    }
                    leftValue = new Operand(leftValue.Value * ofRight.Value.Value, null);
                    continue;
                }
                var right = ParseFactor();
                if (right is null)
                {
                    return null;
                }
                if (token.Kind == TokenKind.Divide)
                {
                    if (right.Value.Value == 0)
                    {
                        return null;
                    }
                    leftValue = new Operand(leftValue.Value / right.Value.Value, null);
                }
                else
                {
                    leftValue = new Operand(leftValue.Value * right.Value.Value, null);
                }
            }
            return leftValue;
        }

        /// <summary>A sign applies to the whole power, which is why -2^2 is -4: the minus is read
        /// last, exactly as it is on paper.</summary>
        private Operand? ParseFactor()
        {
            if (Peek() is { } token && (token.Kind == TokenKind.Minus || token.Kind == TokenKind.Plus))
            {
                Advance();
                var operand = ParseFactor();
                if (operand is null)
                {
                    return null;
                }
                return token.Kind == TokenKind.Minus
                    ? new Operand(-operand.Value.Value, operand.Value.PercentRaw is { } p ? -p : null)
                    : operand.Value;
            }
            return ParsePower();
        }

        private Operand? ParsePower()
        {
            var basis = ParsePrimary();
            if (basis is null)
            {
                return null;
            }
            if (Peek()?.Kind != TokenKind.Power)
            {
                return basis;
            }
            Advance();
            // Powers group to the right: 2^3^2 is 2^9.
            var exponent = ParseFactor();
            if (exponent is null)
            {
                return null;
            }
            var value = Math.Pow(basis.Value.Value, exponent.Value.Value);
            return double.IsFinite(value) ? new Operand(value, null) : null;
        }

        private Operand? ParsePrimary()
        {
            var token = Peek();
            if (token is null)
            {
                return null;
            }
            switch (token.Value.Kind)
            {
                case TokenKind.Number:
                    Advance();
                    if (Peek()?.Kind == TokenKind.Percent)
                    {
                        Advance();
                        return new Operand(token.Value.Number / 100, token.Value.Number);
                    }
                    return new Operand(token.Value.Number, null);
                case TokenKind.LeftParen:
                    Advance();
                    depth++;
                    try
                    {
                        if (depth >= 32)
                        {
                            return null;
                        }
                        var inner = ParseExpression();
                        if (inner is null || Peek()?.Kind != TokenKind.RightParen)
                        {
                            return null;
                        }
                        Advance();
                        if (Peek()?.Kind == TokenKind.Percent)
                        {
                            Advance();
                            return new Operand(inner.Value.Value / 100, inner.Value.Value);
                        }
                        return new Operand(inner.Value.Value, null);
                    }
                    finally
                    {
                        depth--;
                    }
                default:
                    return null;
            }
        }

        private Token? Peek() => index < tokens.Count ? tokens[index] : null;

        private void Advance() => index++;
    }
}
