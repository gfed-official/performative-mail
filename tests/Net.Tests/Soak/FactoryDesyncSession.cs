using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.Soak;

public sealed class FactoryDesyncSession
{
    public const int Seed = 31;

    public static readonly TimeSpan OneWayDelay = TimeSpan.FromMilliseconds(100);

    public const double DropRate = 0.02;

    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    private static readonly AddressId Oak = new(2, 3, 1, 0);

    private readonly int _delayTicks;
    private readonly ConditionedTransport _serverLink;
    private readonly ConditionedTransport _clientLink;
    private readonly ServerRuntime _server;
    private readonly ClientRuntime _client;
    private readonly List<LaneChecksum> _mismatches = new();
    private int _nextItem = 1;
    private int _resends;
    private int _earlyRenders;
    private int _endpointConfirms;

    private FactoryDesyncSession()
    {
        _delayTicks = ConditionedTransport.TicksFor(OneWayDelay);
        var loopback = new LoopbackTransport();
        _serverLink = new ConditionedTransport(loopback.A, OneWayDelay, DropRate, Seed);
        _clientLink = new ConditionedTransport(loopback.B, OneWayDelay, DropRate, Seed ^ unchecked((int)0xA5A5A5A5));
        _server = new ServerRuntime(LoopbackLink.OverPipes(_serverLink));
        _client = new ClientRuntime();
        _client.Connect(_clientLink);
    }

    public static FactoryDesyncReport Run()
    {
        var session = new FactoryDesyncSession();
        session.Join();
        session.CompileFactory();
        session.WaitInterest();
        return session.Pump(SoakDuration.TicksForSimMinutes(10));
    }

    private void Join()
    {
        PumpIdle(_delayTicks * 2);
        if (_client.LocalPlayer is null)
            throw new InvalidOperationException("Client did not join.");
    }

    private void CompileFactory()
    {
        var rows = new ConstructRecord[FactoryDesyncReport.SegmentCount];
        int n = 0;
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 12; x += 2)
            {
                var id = EntityId.FromClassAndCounter(EntityClass.Construct, (uint)(n + 1));
                rows[n] = new ConstructRecord(
                    id,
                    BeltNetwork.BuildingId,
                    new TileCoord(x, y),
                    Facing.East,
                    Owner,
                    80,
                    80);
                n++;
            }
        }

        _server.World.Belts.Compile(rows);
        if (_server.World.Belts.Segments.Count != FactoryDesyncReport.SegmentCount)
            throw new InvalidOperationException($"Compiled {_server.World.Belts.Segments.Count} segments.");
    }

    private void WaitInterest()
    {
        int bound = SegmentInterest.PeriodTicks + _delayTicks * 2 + 2;
        for (int i = 0; i < bound; i++)
        {
            InsertReady();
            TickNet();
            DrainHeads();
            if (AllLive())
                return;
        }

        throw new InvalidOperationException("Client did not enter factory interest.");
    }

    private FactoryDesyncReport Pump(uint ticks)
    {
        float dt = (float)TickClock.TickDurationSeconds;
        for (uint t = 0; t < ticks; t++)
        {
            InsertReady();
            TickNet();
            DrainHeads();
            AdvanceVisual(dt);
            CountEarlyRenders();
        }

        double minutes = ticks / (double)TickClock.TickHz / 60.0;
        double rate = _resends / (double)FactoryDesyncReport.SegmentCount / minutes;
        bool pass = _server.World.Belts.Segments.Count == FactoryDesyncReport.SegmentCount
            && ticks == SoakDuration.TicksForSimMinutes(10)
            && rate <= FactoryDesyncReport.MaxResendsPerSegmentPerMinute
            && _earlyRenders == 0
            && _client.LaneChecksumCount > 0
            && _endpointConfirms > 0;

        return new FactoryDesyncReport
        {
            Segments = _server.World.Belts.Segments.Count,
            TicksRun = ticks,
            ChecksumResends = _resends,
            ChecksumsReceived = _client.LaneChecksumCount,
            EndpointConfirms = _endpointConfirms,
            ResendsPerSegmentPerMinute = rate,
            EarlyEndpointRenders = _earlyRenders,
            Pass = pass
        };
    }

    private void InsertReady()
    {
        var segments = _server.World.Belts.Segments;
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.Lane(0).Count != 0)
                continue;
            segment.TryInsert(0, _nextItem++, 0f, MailKinds.Letter, Oak);
        }
    }

    private void DrainHeads()
    {
        var segments = _server.World.Belts.Segments;
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            for (int lane = 0; lane < BeltNetwork.LaneCount; lane++)
            {
                if (segment.TryTakeHead(lane, out _))
                    _endpointConfirms++;
            }
        }
    }

    private void TickNet()
    {
        _server.TickOnce();
        AdvanceLinks();
        _client.Receive();
        DrainResends();
    }

    private void DrainResends()
    {
        _mismatches.Clear();
        _client.DrainChecksumMismatches(_mismatches);
        for (int i = 0; i < _mismatches.Count; i++)
        {
            var row = _mismatches[i];
            if (_server.ResendLane(row.Segment, row.Lane))
                _resends++;
        }
    }

    private void AdvanceVisual(float dt)
    {
        var segments = _server.World.Belts.Segments;
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            int lengthCm = BeltNetwork.PositionAtTickCm(segment.LengthMetres);
            _client.Lanes.Advance(segment.Id, dt, BeltNetwork.Mk1MetresPerSecond, lengthCm);
        }
    }

    private void CountEarlyRenders()
    {
        var segments = _server.World.Belts.Segments;
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            int lengthCm = BeltNetwork.PositionAtTickCm(segment.LengthMetres);
            for (byte lane = 0; lane < BeltNetwork.LaneCount; lane++)
            {
                var drawn = _client.Lanes.DrawPositions(segment.Id, lane, lengthCm);
                for (int p = 0; p < drawn.Count; p++)
                {
                    if (drawn[p] >= lengthCm)
                        _earlyRenders++;
                }
            }
        }
    }

    private bool AllLive()
    {
        var segments = _server.World.Belts.Segments;
        for (int i = 0; i < segments.Count; i++)
        {
            if (!_client.Lanes.HasSegment(segments[i].Id))
                return false;
        }

        return true;
    }

    private void PumpIdle(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            AdvanceLinks();
            _server.TickOnce();
            _client.Receive();
        }
    }

    private void AdvanceLinks()
    {
        _serverLink.AdvanceTicks(1);
        _clientLink.AdvanceTicks(1);
    }
}
