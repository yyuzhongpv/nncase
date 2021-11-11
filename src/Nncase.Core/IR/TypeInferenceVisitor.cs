﻿// Copyright (c) Canaan Inc. All rights reserved.
// Licensed under the Apache license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nncase.IR
{
    internal sealed class TypeInferenceContext : ITypeInferenceContext
    {
        private readonly Dictionary<Expr, IRType> _exprMemo;

        public TypeInferenceContext(Dictionary<Expr, IRType> exprMemo)
        {
            _exprMemo = exprMemo;
        }

        public Call? CurrentCall { get; set; }

        public Expr GetArgument(Op op, ParameterInfo parameter)
        {
            if (op.GetType() == parameter.OwnerType)
            {
                return GetCurrentCall().Parameters[parameter.Index];
            }
            else
            {
                throw new ArgumentOutOfRangeException($"Operator {op} doesn't have parameter: {parameter.Name}.");
            }
        }

        public IRType GetArgumentType(Op op, ParameterInfo parameter) =>
            _exprMemo[GetArgument(op, parameter)];

        private Call GetCurrentCall() => CurrentCall ?? throw new InvalidOperationException("Current call is not set.");
    }

    internal sealed class TypeInferenceVisitor : ExprVisitor<IRType, IRType>
    {
        private readonly TypeInferenceContext _context;

        public TypeInferenceVisitor()
        {
            _context = new TypeInferenceContext(ExpressionMemo);
        }

        /// <summary>
        /// Gets a value indicating whether is fully inferenced.
        /// </summary>
        public bool IsFullyInferenced { get; private set; } = true;

        public override IRType VisitLeaf(Call expr)
        {
            _context.CurrentCall = expr;
            var type = expr.Target switch
            {
                Function func => ((CallableType)Visit(func)).ReturnType,
                Op op => op.InferInvokeResultTypeNoThrow(_context),
                _ => new InvalidType("Target of call expression should be either a function or an op."),
            };
            _context.CurrentCall = null;
            SetCheckedType(expr, type);
            return type;
        }

        public override IRType VisitLeaf(Const expr)
        {
            var type = expr.ValueType;
            SetCheckedType(expr, type);
            return type;
        }

        public override IRType VisitLeaf(Function expr)
        {
            var paramTypes = expr.Parameters.Select(Visit).ToArray();
            var type = new CallableType(Visit(expr.Body), ImmutableArray.Create(paramTypes));
            SetCheckedType(expr, type);
            return type;
        }

        public override IRType VisitLeaf(Op expr)
        {
            var paramTypes = expr.Parameters.Select(_ => (IRType)AnyType.Default).ToArray();
            var type = new CallableType(AnyType.Default, ImmutableArray.Create(paramTypes));
            SetCheckedType(expr, type);
            return type;
        }

        public override IRType VisitLeaf(Tuple expr)
        {
            var fieldTypes = expr.Fields.Select(Visit).ToArray();
            var type = new TupleType(ImmutableArray.Create(fieldTypes));
            SetCheckedType(expr, type);
            return type;
        }

        public override IRType VisitLeaf(Var expr)
        {
            var type = expr.TypeAnnotation ?? AnyType.Default;
            SetCheckedType(expr, type);
            return type;
        }

        private void SetCheckedType(Expr expr, IRType type)
        {
            expr.CheckedType = type.ThrowIfTypeInferenceInterrupt();
            IsFullyInferenced &= type is not (AnyType or InvalidType);
        }
    }
}
