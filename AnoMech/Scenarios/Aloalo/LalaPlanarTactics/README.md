# Lala Planar Tactics rehearsal

This branch adds a client-side AnoMech rehearsal for Another Aloalo Island's Lala **Planar Tactics** sequence.

## Included scenarios

Under **Another Aloalo Island (test) -> Lala**:

- Planar Tactics — recorded SW (11:50)
- Planar Tactics — recorded NW (11:56)
- Planar Tactics — recorded SW alt (11:58)
- Planar Tactics — recorded NE geometry
- Planar Tactics — synthetic SE mirror

The first three use complete four-player input sets from the supplied BossMod recording. The NE case uses recorded mine geometry with a complete synthetic roster because that pull was missing one player's initial mechanic debuffs. The SE case is the unobserved rotational mirror used by the solver's geometry tests.

## How the rehearsal behaves

- Lala casts Planar Tactics.
- Four selected party seats receive the original mine quota, stack marker, x3/x5 multiplier, and Forward March.
- Eight mine helpers begin the retail Arcane Mine casts.
- Rotation lock-ons arrive later.
- The three simulated participants move to and face their correct assignments.
- The local player is **not** steered by the bot AI unless AnoMech's Debug Bot Control is explicitly enabled.
- At Forced March, all four selected participants are pushed 24 yalms according to their actual current facing plus their x3/x5 rotation.
- Mine crossings decrement the simulated Alpha quota.
- The run checks that all eight mines were consumed and both final stack pairs are within 6 yalms.

A wrong start/facing therefore produces a visible failed rehearsal rather than an automatic pass.

## Companion Splatoon script

Use:

`Extras/Splatoon/Lala_Planar_Tactics_Assignments_v3_ANOMECH_TEST_COPY.txt`

Install the full contents through Splatoon's **Scripts -> Install from Clipboard** and enable:

**ANOMECH REHEARSAL MODE (8-player sim -> 4-player Lala quartet)**

Turn that setting **off** before returning to the real duty.

## Build

AnoMech's upstream build uses .NET 10 and the Dalamud SDK. From the repository root:

```powershell
dotnet build --configuration Release
```

The upstream project expects the Dalamud development files under:

```text
%APPDATA%\XIVLauncher\addon\Hooks\dev\
```

## Load as a dev plugin

After building, in FFXIV open:

**Dalamud Settings -> Experimental -> Dev Plugin Locations**

Add the folder containing the built AnoMech DLL (or the DLL itself). Reload the dev plugin after rebuilding.

## Run the rehearsal

1. Be in an inn, matching AnoMech's normal simulator requirements.
2. Enable the Splatoon v3 helper and its AnoMech rehearsal mode.
3. Open AnoMech with `/anomech`.
4. Select **Another Aloalo Island (test) -> Lala**.
5. Pick one of the five Planar Tactics scenarios.
6. Use the **Bots solve / player practices** strategy.
7. Start the simulation.
8. Follow only the Splatoon start marker and FACE instruction for your character.
9. At the end, AnoMech reports PASS or the reason the rehearsal failed.
10. Exit the simulator before disabling rehearsal mode.

## Important limitations

- The custom zone uses the real AAI territory id and Lala's recorded arena coordinates, but there is no ARealmRecorded zone-in capture available for this duty. The entrance-barrier metadata is intentionally minimal and needs in-game validation.
- This is normal-mode data. Savage is not validated.
- The SE mine orientation was not observed in the supplied recording.
- The simulator validates the strategy model and client-side helper integration; it does not prove exact retail server snapshot timing.
