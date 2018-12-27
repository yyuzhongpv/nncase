﻿using System;
using System.Collections.Generic;
using System.Numerics.Tensors;
using System.Text;

namespace NnCase.Converter.Model.Layers
{
    public class DepthwiseConv2d : Layer
    {
        public InputConnector Input { get; }

        public OutputConnector Output { get; }

        public Tensor<float> Weights { get; }

        public Tensor<float> Bias { get; }

        public Padding Padding { get; }

        public int StrideWidth { get; }

        public int StrideHeight { get; }

        public ActivationFunctionType FusedActivationFunction { get; }

        public int KernelWidth => Weights.Dimensions[3];

        public int KernelHeight => Weights.Dimensions[2];

        public int InputChannels => Weights.Dimensions[1];

        public int OutputChannels => InputChannels;

        public DepthwiseConv2d(ReadOnlySpan<int> dimensions, Tensor<float> weights, Tensor<float> bias, Padding padding, int strideWidth, int strideHeight, ActivationFunctionType fusedActivationFunction)
        {
            Padding = padding;
            StrideWidth = strideWidth;
            StrideHeight = strideHeight;
            FusedActivationFunction = fusedActivationFunction;
            Weights = weights;
            Bias = bias;

            var filterWidth = weights.Dimensions[3];
            var filterHeight = weights.Dimensions[2];

            Input = AddInput("input", dimensions);
            Output = AddOutput("output", new[] {
                dimensions[0],
                dimensions[1],
                (dimensions[2] - (padding == Padding.Valid ? filterHeight - 1 : 0)) / strideHeight,
                (dimensions[3] - (padding == Padding.Valid ? filterWidth - 1 : 0)) / strideWidth
            });
        }

        protected override void OnPlanning(GraphPlanContext context)
        {
            var graph = context.TFGraph;
            var input = context.TFOutputs[Input.Connection.From];
            var weights = Weights.ToHWIO();
            var bias = Bias?.ToNHWC();

            var y = graph.DepthwiseConv2dNative(input, graph.Const(weights),
                new long[] { 1, StrideHeight, StrideWidth, 1 }, Padding.ToString().ToUpperInvariant());
            if (bias != null)
                y = graph.BiasAdd(y, graph.Const(bias));
            context.TFOutputs[Output] = graph.AddActivation(y, FusedActivationFunction);
        }
    }
}