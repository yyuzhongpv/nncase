// Copyright (c) Canaan Inc. All rights reserved.
// Licensed under the Apache license. See LICENSE file in the project root for full license information.

using Nncase.IR;
using OrtKISharp;
using Range = Nncase.IR.Tensors.Range;

namespace Nncase.Evaluator.Tensors;

/// <summary>
/// Evaluator for <see cref="Range"/>.
/// </summary>
public class RangeEvaluator : IEvaluator<Range>, ITypeInferencer<Range>
{
    /// <inheritdoc/>
    public IValue Visit(IEvaluateContext context, Range range)
    {
        var begin = context.GetOrtArgumentValue(range, Range.Begin);
        var end = context.GetOrtArgumentValue(range, Range.End);
        var step = context.GetOrtArgumentValue(range, Range.Step);
        return OrtKI.Range(begin, end, step).ToValue();
    }

    /// <inheritdoc/>
    public IRType Visit(ITypeInferenceContext context, Range target)
    {
        var begin = context.GetArgument(target, Range.Begin);
        var end = context.GetArgument(target, Range.End);
        var step = context.GetArgument(target, Range.Step);
        if (!(begin.CheckedDataType == end.CheckedDataType &&
            end.CheckedDataType == step.CheckedDataType))
        {
            return new InvalidType($"Range Begin End Step must be same type, " +
                                   $"but get begin:{begin.CheckedDataType}," +
                                   $"end:{end.CheckedDataType}," +
                                   $"step:{step.CheckedDataType}");
        }

        var dType = begin.CheckedDataType;
        if ( begin is TensorConst beginValue
             && end is TensorConst endValue
             && step is TensorConst stepValue)
        {

                return new TensorType(
                    dType,
                    new Shape((beginValue.Value.ToScalar<int>() + endValue.Value.ToScalar<int>()) /
                              stepValue.Value.ToScalar<int>()));
        }
        else
        {
            return new TensorType(dType, new Shape(Dimension.Unknown));
        }
    }
}
