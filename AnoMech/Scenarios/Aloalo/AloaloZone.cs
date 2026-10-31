using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Aloalo;

public sealed class AloaloZone : IZone
{
    public static readonly AloaloZone Instance = new();
    public static readonly Phase Lala = new(Instance, "Lala", null, 0);

    public string Name => "Another Aloalo Island (test)";
    public uint TerritoryId => 1179;

    // Retail Lala arena centre is world X=200, Y=-300, Z=0.
    public Vector3 Origin => new(200f, -300f, 0f);

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
    [
        new WaymarkLayout("Lala rehearsal", [
            new(WaymarkSlot.A, new Vector3(  0, 0, -12)),
            new(WaymarkSlot.B, new Vector3( 12, 0,   0)),
            new(WaymarkSlot.C, new Vector3(  0, 0,  12)),
            new(WaymarkSlot.D, new Vector3(-12, 0,   0)),
            new(WaymarkSlot.One,   new Vector3(-12, 0, -12)),
            new(WaymarkSlot.Two,   new Vector3( 12, 0, -12)),
            new(WaymarkSlot.Three, new Vector3( 12, 0,  12)),
            new(WaymarkSlot.Four,  new Vector3(-12, 0,  12)),
        ])
    ];

    // We do not have an ARR zone-in capture for AAI. The dummy barrier exists only to give
    // AnoMech a spawn coordinate. The scenario spawns every actor it needs.
    public DutyInitData DutyInitData { get; } = new(
        new EntranceBarrier(2007457, 0, 0, new Vector3(200f, -300f, 18f), 0f),
        new HelperPool(9020, 20),
        [],
        [],
        []);

    public void Run(SimWorld world)
    {
    }
}
