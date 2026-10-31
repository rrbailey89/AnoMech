using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Aloalo.LalaPlanarTactics;

public enum LalaPlanarPattern
{
    RecordedSwA,
    RecordedNw,
    RecordedSwB,
    RecordedNeGeometry,
    SyntheticSeMirror,
}

public abstract class LalaPlanarTacticsScenarioBase(LalaPlanarPattern pattern) : IScenario
{
    public string Name => pattern switch
    {
        LalaPlanarPattern.RecordedSwA => "Planar Tactics — recorded SW (11:50)",
        LalaPlanarPattern.RecordedNw => "Planar Tactics — recorded NW (11:56)",
        LalaPlanarPattern.RecordedSwB => "Planar Tactics — recorded SW alt (11:58)",
        LalaPlanarPattern.RecordedNeGeometry => "Planar Tactics — recorded NE geometry",
        _ => "Planar Tactics — synthetic SE mirror",
    };

    public IPhase Phase => AloaloZone.Lala;
    public IReadOnlyList<IScenarioAi> AiStrats => [new LalaPlanarTacticsAi()];

    private const uint LalaBaseId = 0x403E;
    private const uint LalaNameId = 12639;
    private const uint GenericHelperBaseId = 9020;

    private const uint ActionPlanarTactics = 34968;
    private const uint ActionArcaneMineAOE = 34970;
    private const uint ActionSymmetricSurge = 34974;

    private const ushort StatusTimes3 = 3721;
    private const ushort StatusSurge = 3723;
    private const ushort StatusAlpha = 3724;
    private const ushort StatusForwardMarch = 3715;
    private const ushort StatusForcedMarch = 3719;
    private const ushort StatusTimes5 = 3790;

    private const float PlanarCastAt = 1.0f;
    private const float DebuffsAt = 6.0f;
    private const float MinesAt = 7.15f;
    private const float RotationIconsAt = 11.90f;
    private const float MarchAt = 21.90f;
    private const float StackAt = 28.10f;

    private SimWorld world = null!;
    private LalaPlanarTacticsState state = null!;
    private SimEnemy? lala;
    private readonly List<SimEnemy> mineCasters = [];
    private readonly HashSet<int> consumedMines = [];
    private readonly Dictionary<PartyRole, int> remainingQuota = [];
    private bool marchStarted;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = LalaPlanarTacticsState.Create(pattern, world.Party.PlayerRole);
        marchStarted = false;
        consumedMines.Clear();
        remainingQuota.Clear();
        mineCasters.Clear();

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<LalaPlanarTacticsState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnActors);
        world.Events.Add(0.2f, MoveUnusedSeatsAway);
        world.Events.Add(PlanarCastAt, () => lala?.Cast(ActionPlanarTactics));
        world.Events.Add(DebuffsAt, ApplyDebuffs);
        world.Events.Add(MinesAt - 0.35f, SpawnMineActors);
        world.Events.Add(MinesAt, CastMines);
        world.Events.Add(RotationIconsAt, ApplyRotationIcons);
        world.Events.Add(MarchAt, BeginForcedMarch);
        world.Events.Add(StackAt, ResolveStacks);
        world.Events.Add(StackAt + 3f, () => lala?.Despawn());
    }

    public void Tick(float delta, float elapsed)
    {
        if (!marchStarted || elapsed >= StackAt || consumedMines.Count >= state.Mines.Length) return;

        for (var i = 0; i < state.Mines.Length; i++)
        {
            if (consumedMines.Contains(i)) continue;
            var mine = state.Mines[i];
            foreach (var spec in state.Members)
            {
                if (world.Party.Get(spec.Role) is not { } member || !member.IsAlive()) continue;
                if (MathF.Abs(member.Position.X - mine.X) > 4f || MathF.Abs(member.Position.Z - mine.Z) > 4f) continue;

                consumedMines.Add(i);
                var left = Math.Max(0, remainingQuota.GetValueOrDefault(spec.Role) - 1);
                remainingQuota[spec.Role] = left;
                if (left == 0) member.RemoveStatus(StatusAlpha);
                else member.AddStatus(StatusAlpha, StackAt - elapsed + 1f, left, overrideStacks: true);
                world.Announce($"{DisplayRole(spec.Role)} consumed mine {i + 1}; remaining quota {left}.");
                break;
            }
        }
    }

    private void SpawnActors()
    {
        lala = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: LalaBaseId,
            NameId: LalaNameId,
            Level: 90,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    private void MoveUnusedSeatsAway()
    {
        var focus = state.Members.Select(m => m.Role).ToHashSet();
        var spare = 0;
        foreach (PartyRole role in Enum.GetValues<PartyRole>())
        {
            if (focus.Contains(role) || world.Party.Get(role) is not { } member) continue;
            var z = -18f + 6f * spare++;
            member.SetPosition(new Vector3(31f, 0f, z));
        }
    }

    private void ApplyDebuffs()
    {
        foreach (var spec in state.Members)
        {
            if (world.Party.Get(spec.Role) is not { } member) continue;
            remainingQuota[spec.Role] = spec.Quota;
            member.AddStatusParam(StatusAlpha, spec.Quota, StackAt - DebuffsAt + 1f);
            if (spec.StackTarget) member.AddStatus(StatusSurge, StackAt - DebuffsAt + 1f);
            member.AddStatus(spec.Multiplier == 3 ? StatusTimes3 : StatusTimes5, StackAt - DebuffsAt + 1f);
            member.AddStatus(StatusForwardMarch, MarchAt - DebuffsAt);
        }
        world.Announce($"Planar Tactics rehearsal: {state.Label}. Starts can now be solved; rotation arrows follow later.");
    }

    private void SpawnMineActors()
    {
        foreach (var pos in state.Mines)
        {
            var helper = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: GenericHelperBaseId,
                Level: 1,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                Visibility: SpawnVisibility.InvisibleHelper,
                Placement: new Placement(pos, 0f),
                Engaged: false));
            if (helper != null) mineCasters.Add(helper);
        }
    }

    private void CastMines()
    {
        foreach (var helper in mineCasters)
            helper.Cast(ActionArcaneMineAOE, animationLock: 0.1f);
    }

    private void ApplyRotationIcons()
    {
        foreach (var spec in state.Members)
            world.Party.Get(spec.Role)?.AttachLockonVfx(spec.Icon);
        world.Announce("Rotation arrows applied. Follow the helper's FACE instruction; the bots will demonstrate their own facings.");
    }

    private void BeginForcedMarch()
    {
        marchStarted = true;
        foreach (var spec in state.Members)
        {
            if (world.Party.Get(spec.Role) is not { } member || !member.IsAlive()) continue;
            member.AddStatus(StatusForcedMarch, 6f);
            var postRotation = member.Rotation + LalaPlanarTacticsState.NetRotation(spec.Multiplier, spec.Icon);
            member.Push(postRotation, 24f, 6f);
        }
    }

    private void ResolveStacks()
    {
        var failures = new List<string>();
        if (consumedMines.Count != 8) failures.Add($"only {consumedMines.Count}/8 mines were consumed");

        foreach (var spec in state.Members)
        {
            var left = remainingQuota.GetValueOrDefault(spec.Role);
            if (left != 0) failures.Add($"{DisplayRole(spec.Role)} has {left} mine(s) left");
        }

        foreach (var pair in state.Pairs)
        {
            if (world.Party.Get(pair.A) is not { } a || world.Party.Get(pair.B) is not { } b) continue;
            var dist = FlatDistance(a.Position, b.Position);
            if (dist > 6f) failures.Add($"{DisplayRole(pair.A)} + {DisplayRole(pair.B)} are {dist:F1}y apart (need <=6)");
        }

        foreach (var spec in state.Members.Where(m => m.StackTarget))
            if (world.Party.Get(spec.Role) is { } target)
                lala?.CastFromHelper(ActionSymmetricSurge, target, at: new Placement(target.Position, 0f), animationLock: 0.1f);

        if (failures.Count == 0)
            world.Announce("Planar Tactics rehearsal PASS: all 8 mines consumed and both pairs stacked.");
        else
            world.Fail("Planar Tactics rehearsal failed: " + string.Join("; ", failures), freeze: true);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
        => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    private static string DisplayRole(PartyRole role) => role.ToString();
}

public sealed class LalaPlanarRecordedSwAScenario() : LalaPlanarTacticsScenarioBase(LalaPlanarPattern.RecordedSwA);
public sealed class LalaPlanarRecordedNwScenario() : LalaPlanarTacticsScenarioBase(LalaPlanarPattern.RecordedNw);
public sealed class LalaPlanarRecordedSwBScenario() : LalaPlanarTacticsScenarioBase(LalaPlanarPattern.RecordedSwB);
public sealed class LalaPlanarRecordedNeGeometryScenario() : LalaPlanarTacticsScenarioBase(LalaPlanarPattern.RecordedNeGeometry);
public sealed class LalaPlanarSyntheticSeMirrorScenario() : LalaPlanarTacticsScenarioBase(LalaPlanarPattern.SyntheticSeMirror);

public sealed class LalaPlanarTacticsAi : IScenarioAi<LalaPlanarTacticsState>
{
    public string Name => "Bots solve / player practices";

    public void Run(LalaPlanarTacticsState state, SimWorld world)
    {
        // AI orders do not steer the real player unless AnoMech Debug Bot Control is explicitly enabled.
        world.Events.Add(7.8f, () =>
        {
            foreach (var spec in state.Members)
                world.Party.Get(spec.Role)?.MoveTo(spec.Start, speed: 8.3f);
        });

        world.Events.Add(12.25f, () =>
        {
            foreach (var spec in state.Members)
                world.Party.Get(spec.Role)?.TurnTo(spec.FaceHeading);
        });
    }
}

public sealed class LalaPlanarTacticsState
{
    public sealed record Member(
        PartyRole Role,
        int Quota,
        bool StackTarget,
        int Multiplier,
        uint Icon,
        int Route,
        Vector3 Start,
        Vector3 March,
        Vector3 End,
        float FaceHeading);

    public sealed record Pair(PartyRole A, PartyRole B);

    private static readonly Vector3[] BaseStarts =
    [
        new(-13f, 0f, 5f),
        new(-13f, 0f, 3f),
        new(-16f, 0f, -8f),
        new(8f, 0f, 13f),
    ];

    private static readonly Vector3[] BaseMarches =
    [
        new(1f, 0f, 0f),
        new(1f, 0f, 0f),
        new(1f, 0f, 0f),
        new(0f, 0f, -1f),
    ];

    public required string Label { get; init; }
    public required int Rotation { get; init; }
    public required Vector3[] Mines { get; init; }
    public required Member[] Members { get; init; }
    public required Pair[] Pairs { get; init; }

    public static LalaPlanarTacticsState Create(LalaPlanarPattern pattern, PartyRole localRole)
    {
        var focus = FocusRoles(localRole);
        var rotation = pattern switch
        {
            LalaPlanarPattern.RecordedSwA => 0,
            LalaPlanarPattern.RecordedNw => 1,
            LalaPlanarPattern.RecordedSwB => 0,
            LalaPlanarPattern.RecordedNeGeometry => 2,
            _ => 3,
        };

        var raw = pattern switch
        {
            LalaPlanarPattern.RecordedSwA => new[]
            {
                new Raw(3, false, 5, 494u), new Raw(2, true, 3, 493u),
                new Raw(2, false, 5, 494u), new Raw(1, true, 5, 494u),
            },
            LalaPlanarPattern.RecordedNw => new[]
            {
                new Raw(1, false, 3, 494u), new Raw(3, true, 5, 493u),
                new Raw(2, true, 3, 493u), new Raw(2, false, 3, 494u),
            },
            LalaPlanarPattern.RecordedSwB => new[]
            {
                new Raw(2, true, 5, 494u), new Raw(2, false, 3, 494u),
                new Raw(3, false, 5, 494u), new Raw(1, true, 3, 494u),
            },
            LalaPlanarPattern.RecordedNeGeometry => new[]
            {
                new Raw(3, false, 5, 494u), new Raw(2, true, 3, 493u),
                new Raw(2, false, 5, 494u), new Raw(1, true, 5, 494u),
            },
            _ => new[]
            {
                new Raw(1, false, 3, 494u), new Raw(3, true, 5, 493u),
                new Raw(2, true, 3, 493u), new Raw(2, false, 3, 494u),
            },
        };

        var assigned = focus.Select((role, i) => new Mutable(role, raw[i])).ToArray();
        var one = assigned.Single(x => x.Raw.Quota == 1);
        var three = assigned.Single(x => x.Raw.Quota == 3);
        var twos = assigned.Where(x => x.Raw.Quota == 2).ToArray();
        var twoA = twos.Single(x => x.Raw.StackTarget != one.Raw.StackTarget);
        var twoB = twos.Single(x => x.Role != twoA.Role);
        if (three.Raw.StackTarget == twoB.Raw.StackTarget)
            throw new InvalidOperationException("Fixture has no valid marked/unmarked pair for THREE.");

        one.Route = 0;
        twoA.Route = 1;
        twoB.Route = 2;
        three.Route = 3;

        var members = assigned.Select(x => BuildMember(x.Role, x.Raw, x.Route, rotation)).ToArray();
        var byRoute = members.ToDictionary(x => x.Route);
        return new LalaPlanarTacticsState
        {
            Label = pattern switch
            {
                LalaPlanarPattern.RecordedSwA => "recorded complete SW pattern (11:50)",
                LalaPlanarPattern.RecordedNw => "recorded complete NW pattern (11:56)",
                LalaPlanarPattern.RecordedSwB => "recorded complete SW alternate debuffs (11:58)",
                LalaPlanarPattern.RecordedNeGeometry => "recorded NE mine geometry + complete synthetic roster",
                _ => "synthetic SE rotational mirror",
            },
            Rotation = rotation,
            Mines = BuildMines(rotation),
            Members = members,
            Pairs = [new Pair(byRoute[0].Role, byRoute[1].Role), new Pair(byRoute[2].Role, byRoute[3].Role)],
        };
    }

    public static float NetRotation(int multiplier, uint icon)
    {
        var sign = icon == 494u ? 1f : icon == 493u ? -1f : 0f;
        var mult = multiplier == 3 ? -1f : multiplier == 5 ? 1f : 0f;
        return sign * mult * MathF.PI / 2f;
    }

    private static Member BuildMember(PartyRole role, Raw raw, int route, int rotation)
    {
        var start = Rotate(BaseStarts[route], rotation);
        var march = Rotate(BaseMarches[route], rotation);
        var end = start + march * 24f;
        var marchHeading = MathF.Atan2(march.X, march.Z);
        var face = Normalize(marchHeading - NetRotation(raw.Multiplier, raw.Icon));
        return new Member(role, raw.Quota, raw.StackTarget, raw.Multiplier, raw.Icon, route, start, march, end, face);
    }

    private static Vector3[] BuildMines(int rotation)
    {
        var mines = new List<Vector3>();
        for (var x = -8; x <= 8; x += 8)
            for (var z = -8; z <= 8; z += 8)
            {
                if (x == -8 && z == 8) continue;
                mines.Add(Rotate(new Vector3(x, 0f, z), rotation));
            }
        return mines.ToArray();
    }

    private static PartyRole[] FocusRoles(PartyRole localRole)
    {
        var tank = localRole.IsTank() ? localRole : PartyRole.MainTank;
        var healer = IsHealer(localRole) ? localRole : PartyRole.ShieldHealer;
        var dps = new List<PartyRole>();
        if (localRole.IsDps()) dps.Add(localRole);
        foreach (var role in new[] { PartyRole.PhysRangedDps, PartyRole.CasterDps, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB })
            if (!dps.Contains(role) && dps.Count < 2) dps.Add(role);
        return [tank, healer, dps[0], dps[1]];
    }

    private static bool IsHealer(PartyRole role) => role is PartyRole.RegenHealer or PartyRole.ShieldHealer;

    private static Vector3 Rotate(Vector3 p, int n)
    {
        for (var i = 0; i < ((n % 4) + 4) % 4; i++) p = new Vector3(-p.Z, p.Y, p.X);
        return p;
    }

    private static float Normalize(float x) => MathF.Atan2(MathF.Sin(x), MathF.Cos(x));

    private sealed record Raw(int Quota, bool StackTarget, int Multiplier, uint Icon);
    private sealed class Mutable(PartyRole role, Raw raw)
    {
        public PartyRole Role { get; } = role;
        public Raw Raw { get; } = raw;
        public int Route { get; set; }
    }
}
