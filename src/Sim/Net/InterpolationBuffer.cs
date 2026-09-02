using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.Net;

public sealed class InterpolationBuffer
{
    public const int Capacity = 3;

    // chapter 06 §3: interpolation buffer 100 ms (3 snapshots). Do not invent another holdback.
    public static readonly TimeSpan Holdback = TimeSpan.FromMilliseconds(100);

    private readonly List<Sample> _samples = new List<Sample>(Capacity);

    public int Count => _samples.Count;

    public static InterpolationBuffer ForRemote(EntityId remote, EntityId owner)
    {
        if (remote == owner)
            throw new InvalidOperationException("Owner pawn must not enter InterpolationBuffer.");

        return new InterpolationBuffer();
    }

    public static TimeSpan TimeOfTick(uint tick) =>
        TimeSpan.FromSeconds(tick / (double)TickClock.TickHz);

    public void Push(in RemoteSnapshot snapshot) =>
        Push(snapshot.ServerTime, snapshot.Pose);

    public void Push(in OwnerSnapshot snapshot)
    {
        throw new InvalidOperationException("Owner pawn must not enter InterpolationBuffer.");
    }

    public void Push(TimeSpan serverTime, in PlayerPose pose)
    {
        for (int i = 0; i < _samples.Count; i++)
        {
            if (_samples[i].Time == serverTime)
            {
                _samples[i] = new Sample(serverTime, pose);
                return;
            }
        }

        _samples.Add(new Sample(serverTime, pose));
        _samples.Sort(CompareTime);
        if (_samples.Count > Capacity)
            _samples.RemoveAt(0);
    }

    public bool TryPresent(TimeSpan now, out PlayerPose pose) =>
        TrySample(now - Holdback, out pose);

    private bool TrySample(TimeSpan present, out PlayerPose pose)
    {
        pose = default;
        if (_samples.Count == 0)
            return false;

        if (present <= _samples[0].Time)
        {
            pose = _samples[0].Pose;
            return true;
        }

        var newest = _samples[_samples.Count - 1];
        if (present >= newest.Time)
        {
            pose = newest.Pose;
            return true;
        }

        for (int i = 0; i < _samples.Count - 1; i++)
        {
            var left = _samples[i];
            var right = _samples[i + 1];
            if (present > right.Time)
                continue;

            var span = right.Time - left.Time;
            if (span <= TimeSpan.Zero)
            {
                pose = right.Pose;
                return true;
            }

            var t = (present - left.Time).TotalSeconds / span.TotalSeconds;
            var leftPose = left.Pose;
            var rightPose = right.Pose;
            pose = Lerp(leftPose, rightPose, t);
            return true;
        }

        pose = newest.Pose;
        return true;
    }

    private static int CompareTime(Sample left, Sample right) =>
        left.Time.CompareTo(right.Time);

    private static PlayerPose Lerp(PlayerPose left, PlayerPose right, double t) =>
        new(
            LerpCm(left.Xcm, right.Xcm, t),
            LerpCm(left.Ycm, right.Ycm, t),
            LerpCm(left.Zcm, right.Zcm, t),
            LerpYaw(left.Yaw, right.Yaw, t));

    private static int LerpCm(int start, int end, double t) =>
        (int)Math.Round(start + (end - start) * t, MidpointRounding.AwayFromZero);

    private static ushort LerpYaw(ushort start, ushort end, double t)
    {
        int delta = end - start;
        if (delta > 32768)
            delta -= 65536;
        if (delta < -32768)
            delta += 65536;

        int yaw = start + (int)Math.Round(delta * t, MidpointRounding.AwayFromZero);
        return (ushort)(yaw & 0xFFFF);
    }

    private readonly struct Sample
    {
        public Sample(TimeSpan time, PlayerPose pose)
        {
            Time = time;
            Pose = pose;
        }

        public TimeSpan Time { get; }

        public PlayerPose Pose { get; }
    }
}
