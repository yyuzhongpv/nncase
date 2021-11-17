using System;
using Nncase.IR.Math;
using TorchSharp;

using torchF = TorchSharp.torch.nn.functional;
namespace Nncase.Evaluator.Ops
{
    public sealed partial class EvaluatorVisitor
    {
        private torch.Tensor VisitUnary(Unary unary)
        {
            var i = _context.GetArgument(unary, Unary.Input);
            return unary.UnaryOp switch
            {
                UnaryOp.Abs => torch.abs(i),
                UnaryOp.Ceil => torch.ceil(i),
                UnaryOp.Cos => torch.cos(i),
                UnaryOp.Exp => torch.exp(i),
                UnaryOp.Floor => torch.floor(i),
                UnaryOp.Log => torch.log(i),
                UnaryOp.Neg => torch.neg(i),
                UnaryOp.Round => torch.round(i),
                UnaryOp.Rsqrt => torch.rsqrt(i),
                UnaryOp.Sin => torch.sin(i),
                UnaryOp.Sqrt => torch.sqrt(i),
                UnaryOp.Square => torch.square(i),
                UnaryOp.Tanh => torch.tanh(i),
                UnaryOp.BitwiseNot => torch.bitwise_not(i),
                UnaryOp.LogicalNot => torch.logical_not(i),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}