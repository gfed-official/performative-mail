using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using Xunit;

namespace PerformativeMail.Sim.Tests.Movement;

public sealed class MovementStepTests
{
    private static readonly double Dt = TickClock.TickDurationSeconds;

    [Fact]
    public void Context_UsesChapter11Section8SpeedsAndWeightFloor()
    {
        Assert.Equal(5.0f, MovementContext.WalkMetersPerSecond);
        Assert.Equal(7.5f, MovementContext.SprintMetersPerSecond);
        Assert.Equal(0.6f, MovementContext.WeightSpeedFloor);
        Assert.Equal(1.15f, MovementStep.DisplacementClampFactor);
        Assert.Equal(0.6f, new MovementContext(40).WeightMultiplier);
        Assert.Equal(0.6f, new MovementContext(100).WeightMultiplier);
        Assert.Equal(1.0f, MovementContext.Unburdened.WeightMultiplier);
    }

    [Fact]
    public void ForwardWalk_OneTick_MovesFiveMetresPerSecondThenQuantises()
    {
        var pose = MovementStep.ApplyTick(
            PlayerPose.Origin,
            Forward(InputButtons.None),
            MovementContext.Unburdened);

        var expectedY = PlayerPose.QuantizeCm(5.0 / 30.0);
        Assert.Equal(17, expectedY);
        Assert.Equal(0, pose.Xcm);
        Assert.Equal(expectedY, pose.Ycm);
        Assert.Equal(0, pose.Zcm);
        Assert.Equal((ushort)0, pose.Yaw);
    }

    [Fact]
    public void Sprint_OneTick_UsesSevenPointFiveMetresPerSecond()
    {
        var pose = MovementStep.ApplyTick(
            PlayerPose.Origin,
            Forward(InputButtons.Sprint),
            MovementContext.Unburdened);

        Assert.Equal(25, PlayerPose.QuantizeCm(7.5 / 30.0));
        Assert.Equal(0, pose.Xcm);
        Assert.Equal(25, pose.Ycm);
        Assert.Equal(0, pose.Zcm);
    }

    [Fact]
    public void WeightPoints_AtFloor_ScalesWalkToZeroPointSix()
    {
        var pose = MovementStep.ApplyTick(
            PlayerPose.Origin,
            Forward(InputButtons.None),
            new MovementContext(40));

        Assert.Equal(0.6f, new MovementContext(40).WeightMultiplier);
        Assert.Equal(10, PlayerPose.QuantizeCm(5.0 * 0.6 / 30.0));
        Assert.Equal(0, pose.Xcm);
        Assert.Equal(10, pose.Ycm);
        Assert.Equal(0, pose.Zcm);
    }

    [Fact]
    public void Displacement_AboveMaxSpeedTimes1_15_IsClamped()
    {
        var unclamped = 5.0 * Math.Sqrt(2.0) * Dt;
        var maxDisp = 5.0 * 1.15 * Dt;
        Assert.True(unclamped > maxDisp);

        var pose = MovementStep.ApplyTick(
            PlayerPose.Origin,
            new InputCmd(0, MovementStep.AxisFull, MovementStep.AxisFull, 0, InputButtons.None),
            MovementContext.Unburdened);

        var expected = PlayerPose.QuantizeCm(maxDisp / Math.Sqrt(2.0));
        Assert.Equal(expected, pose.Xcm);
        Assert.Equal(expected, pose.Ycm);
        Assert.Equal(0, pose.Zcm);

        var walked = MovementStep.ApplyTick(
            PlayerPose.Origin,
            Forward(InputButtons.None),
            MovementContext.Unburdened);
        Assert.Equal(17, walked.Ycm);
        Assert.True(5.0 / 30.0 < maxDisp);
    }

    [Fact]
    public void Apply_CopiesYawAndLeavesHeightUnchanged()
    {
        var start = new PlayerPose(0, 0, 12, 0);
        var pose = MovementStep.ApplyTick(
            start,
            new InputCmd(3, 0, 0, 12345, InputButtons.Jump),
            MovementContext.Unburdened);

        Assert.Equal(0, pose.Xcm);
        Assert.Equal(0, pose.Ycm);
        Assert.Equal(12, pose.Zcm);
        Assert.Equal((ushort)12345, pose.Yaw);
    }

    [Fact]
    public void Forward_AtEastYaw_MovesPositiveX()
    {
        const ushort east = 16384;
        var pose = MovementStep.ApplyTick(
            PlayerPose.Origin,
            new InputCmd(0, 0, MovementStep.AxisFull, east, InputButtons.None),
            MovementContext.Unburdened);

        Assert.Equal(17, pose.Xcm);
        Assert.Equal(0, pose.Ycm);
        Assert.Equal(0, pose.Zcm);
        Assert.Equal(east, pose.Yaw);
    }

    private static InputCmd Forward(InputButtons buttons) =>
        new(0, 0, MovementStep.AxisFull, 0, buttons);
}
