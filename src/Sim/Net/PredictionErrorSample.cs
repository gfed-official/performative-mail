using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.Net;

public readonly record struct PredictionErrorSample(
    int PredictedXcm,
    int PredictedYcm,
    int ServerXcm,
    int ServerYcm)
{
    public PredictionErrorSample(in PlayerPose predicted, in PlayerPose server)
        : this(predicted.Xcm, predicted.Ycm, server.Xcm, server.Ycm)
    {
    }

    public int DeltaXcm => PredictedXcm - ServerXcm;

    public int DeltaYcm => PredictedYcm - ServerYcm;

    public double HorizontalErrorSquared
    {
        get
        {
            var dx = (double)DeltaXcm;
            var dy = (double)DeltaYcm;
            return dx * dx + dy * dy;
        }
    }

    public static double HorizontalDistanceCm(in PlayerPose left, in PlayerPose right)
    {
        var dx = (double)(left.Xcm - right.Xcm);
        var dy = (double)(left.Ycm - right.Ycm);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static double RmsCentimetres(IReadOnlyList<PredictionErrorSample> samples)
    {
        if (samples is null)
            throw new ArgumentNullException(nameof(samples));
        if (samples.Count == 0)
            throw new ArgumentException("RMS needs at least one sample.", nameof(samples));

        double sumSquares = 0;
        for (int i = 0; i < samples.Count; i++)
            sumSquares += samples[i].HorizontalErrorSquared;

        return Math.Sqrt(sumSquares / samples.Count);
    }
}
