#!/usr/bin/env python3
"""Generate docs/MODULE_MAP.md: every source file of the six reference FCS plugins, what layer it belongs to, how big it is
on each jet and how far the jets have diverged from each other (namespace differences ignored).

usage: python3 tools/gen_module_map.py [kit_root]
"""
import os, re, sys, difflib

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..")
# in fork order: each jet was forked from the one before it (F-22E -> Ifrit -> Revoker -> Compass -> Ternion -> Su-30)
JETS = [("F-22E", "reference/F22E-FCS-4.3.0/Aryx_F22E_StrikeRaptor.FCS", "Aryx_F22E_StrikeRaptor.FCS"),
        ("Ifrit", "reference/Ifrit-FCS-1.3.0/Ifrit_KR67.FCS", "Ifrit_KR67.FCS"),
        ("Revoker", "reference/Revoker-FCS-1.4.1/Revoker_FS12.FCS", "Revoker_FS12.FCS"),
        ("Compass", "reference/Compass-FCS-0.5.0/Compass_TA30.FCS", "Compass_TA30.FCS"),
        ("Ternion", "reference/Ternion-FCS-0.4.0/Ternion_FS3.FCS", "Ternion_FS3.FCS"),
        ("Su-30", "reference/Su30-FCS-0.6.0/Su30_VXN.FCS", "Su30_VXN.FCS")]

# layer, one-line role. Layers:
#   LAW      control laws / allocation / estimation - the FCS itself
#   MODEL    what the FCS believes about the airframe (effector models, foils, mass, aero mirrors)
#   AERO     airframe aerodynamic modifications applied to the GAME (the FCS models mirror them)
#   GLUE     integration with the game: Harmony hooks, aircraft detection, setup, config, multiplayer, API
#   SYSTEM   aircraft systems the FCS also runs (fuel balance)
#   EXTRA    gameplay / cosmetic extras, not needed for flight control
ROLE = {
    "FcsController.cs": ("LAW", "The whole flight control computer: air data, modes, pitch law, lateral law, reference-capacity governor, push plan, limits (asym/low-thrust/fin-loss/engine-out), INDI, allocator calls, ground steering, VMC, telemetry. Per-jet."),
    "Allocator.cs": ("LAW", "Nonlinear control allocator: box-constrained Gauss-Newton over every effector's moment table; regularisation, pair costs, fight costs, reach pass + two-horizon pick, paired stab/TVC search, slip-frame weighting."),
    "CapacityEstimator.cs": ("LAW", "Per-axis, per-side angular-acceleration capacity (measured + committed + allocator probe). The governor's only input about what the jet can still do."),
    "RateTraj.cs": ("LAW", "Acceleration- and jerk-limited trajectory toward a target (command shaping of the pitch-rate target)."),
    "EstimatorJob.cs": ("LAW", "Threading (F-22E 3.0.4): runs the capacity estimator's allocator probes on a pool thread, on mirrored effectors and allocator; joined at the top of the next tick (the laws already read the previous tick's estimate)."),
    "ParallelFcs.cs": ("LAW", "Threading (F-22E 3.1.0): the shared pool rules (FcsPool.Helpers = 1-3 helpers per jet), Claimable jobs the main thread can run inline, IndexFork fork-join, EffectorMirror copies."),
    "TickStats.cs": ("GLUE", "Per-phase tick timer (TickTimerLog): mean / median / p95 / max per tick, written to LogOutput.log every 10 s."),
    "TelemetryRow.cs": ("GLUE", "One CSV row captured as format strings + boxed values on the main thread, formatted and written by the background writer thread (F-22E 3.0.4)."),
    "Strake.cs": ("AERO", "Compass / Ternion / Su-30: slender-strake (LERX) Polhamus vortex-lift curve with breakdown (StrakeVortexLift)."),
    "CantedTvc.cs": ("MODEL", "Su-30: the AL-31FP single-axis nozzles canted 32 deg outboard. Each nozzle is one effector axis; symmetric = pitch, differential = proverse roll + yaw."),
    "Names.cs": ("GLUE", "Su-30: maps the blueprint's L/R-prefixed part names (LCanard_struct, Left_stab_struct...) onto the lineage's _L/_R suffix scheme."),
    "RaptorFoil.cs": ("AERO", "Su-30: the F-22E's stab lift curve (Aryx's F22E_Wing remapped to a 25 deg peak) put on the Su-30's stabs (RaptorStabFoil)."),
    "StructureFix.cs": ("GLUE", "Ternion: re-homes a part whose transform parent is an engine but which is jointed to the airframe, so a destroyed engine no longer takes the tail with it."),
    "ThrustLine.cs": ("AERO", "Su-30: moves each nozzle's thrust force point to the CG's height (ThrustLineAtCg); the FCS's nozzle models use the same arm."),
    "CoupledCap.cs": ("LAW", "Coupled asymmetric-load check (Ternion 0.2.2, all six since 12 Nov): per sweep AoA a small LP over every effector's moment points gives the flight-path roll left each way with the load, pitch held and no sideslip acceleration; the AoA cap sits where the weaker side falls to AsymCoupledReserveMax of the roll range (FindTop = the lower edge of the top over-threshold stretch). Off by default on the F-22E."),
    "FcsNative.cs": ("GLUE", "Native (Rust) core wrapper (F-22E 4.0.0+, all six): DllImport of <Jet>_FcsCore (allocator solve, grid kernels, coupled-check LP), ABI check, load-time self-tests against C# twins, extraction of the embedded core to BepInEx/cache/<Lib>/<sha>/, C# fallback."),
    "PitchBump.cs": ("AERO", "Su-30 0.2.5: extra lift on the body sections ahead of the CG at high AoA (HighAoAPitchBump) against the pitch wall."),
    "Lpf.cs": ("LAW", "First-order low-pass filter (scalar)."),
    "Lpf3.cs": ("LAW", "First-order low-pass filter (Vector3)."),
    "FlapSystem.cs": ("SYSTEM", "High-lift devices and trailing-edge droop: the flap schedule, slats, deceleron/HLD folding, transforms the FCS drives directly, droop-to-stall headroom."),
    "FcsMode.cs": ("LAW", "Mode enum: Off, Ground, GearDownRate, AoAG, MPO, AIRate, Rate."),
    "AsymCap.cs": ("LAW", "Asymmetric-load AoA cap: remaps the stick onto a reduced AoA limit when stores/damage leave too little lateral authority."),
    "Effector.cs": ("MODEL", "Abstract effector: travel, servo rate, costs, moment table M[grid] (roll, pitch, yaw), live-node anchoring, pair/fight groups."),
    "SurfaceEffector.cs": ("MODEL", "A game ControlSurface as an effector: its plate's moment at local AoA across its travel (game plate equation + airfoil), stall/reversal-aware, camber + downwash mirrors."),
    "TvcEffector.cs": ("MODEL", "Pitch thrust vectoring (symmetric nozzle angle) as an effector, from live thrust."),
    "TvcYawEffector.cs": ("MODEL", "Yaw thrust vectoring (Revoker's single nozzle) as an effector about the pitch angle. Carried by the Revoker's descendants."),
    "IfritEffectors.cs": ("MODEL", "SplitEffector (split decelerons: drag yaw) and TvcDiffEffector (differential/roll TVC about the symmetric angle)."),
    "HingeFlapEffector.cs": ("MODEL", "Revoker's inboard elevons: a hinged trailing-edge surface that is not a ControlSurface, driven and modelled by the FCS."),
    "SurfaceKind.cs": ("MODEL", "Surface roles: Stabilator, Flaperon, Aileron, Rudder, Other."),
    "EvalContext.cs": ("MODEL", "Per-tick context passed to Effector.Evaluate (air density, root rotation, wind...)."),
    "AirfoilTable.cs": ("MODEL", "Lookup table of an airfoil's CL/CD vs AoA as the game samples it; knee / peak helpers."),
    "Acc.cs": ("GLUE", "Accessors: reflection/AccessTools field refs into the game's private fields (ControlSurface, AeroPart, Turbojet, jobs...)."),
    "StallStagger.cs": ("AERO", "Staggered stall: remapped airfoil copies per surface family (the game gets them via StallStaggerJobs; the FCS models use TableFor)."),
    "StallStaggerJobs.cs": ("AERO", "Injects the staggered/reshaped foils into the game's aero job chart array (AeroJobSettings.Schedule prefix)."),
    "LiftShape.cs": ("AERO", "F-22E only: positive-AoA lift reshaping of wing and fuselage parts."),
    "BodyLift.cs": ("AERO", "Fuselage/body lift curve helpers used by the stagger/lift-shape foils."),
    "Camber.cs": ("AERO", "Flap-camber coupling model: which fixed part hosts each surface, K per surface (thin-airfoil flap theory)."),
    "CamberForce.cs": ("AERO", "Applies the camber-coupling correction force to the game's host part each physics step."),
    "Downwash.cs": ("AERO", "Stab-in-wing-downwash model (F-22E wake-exit tail). Mirrored in SurfaceEffector."),
    "DownwashForce.cs": ("AERO", "Applies the downwash correction to the game (F-22E only attaches it)."),
    "DragRelief.cs": ("EXTRA", "Performance profile: low-AoA drag relief / transonic relief (gameplay buff)."),
    "Countermeasures.cs": ("EXTRA", "Flare / gun-ammo / EW capacity scaling per performance profile (gameplay)."),
    "NozzleRig.cs": ("EXTRA", "F-22E only: cosmetic re-rig of Aryx's nozzle blades (hinged petals)."),
    "NozzleKinematics.cs": ("EXTRA", "F-22E only: nozzle petal kinematics for the cosmetic rig."),
    "NozzleMeshData.cs": ("EXTRA", "F-22E only: generated split nozzle mesh (derived from Aryx's mesh)."),
    "VisualHinge.cs": ("EXTRA", "Revoker only: draws trailing-edge surfaces about their modelled hinge line (render only)."),
    "FuelBalance.cs": ("SYSTEM", "Fuel transfer between healthy tanks to hold the CG (and lateral fuel trim when yaw/roll authority runs short)."),
    "Hooks.cs": ("GLUE", "Harmony patch bodies: ControlsFilter.Filter (the FCS tick), ControlSurface write-back, TVC transpilers, gear steering, flaps, airbrake, AI auto-aim."),
    "Registry.cs": ("GLUE", "Which aircraft get the FCS (IsF22 = aircraft match), one-time airframe setup, controller lifetime, surface ownership."),
    "Plugin.cs": ("GLUE", "All config keys (player + Dev file), migrations, Harmony installation."),
    "FcsStandalonePlugin.cs": ("GLUE", "The BepInEx plugin class (GUID, version, Awake)."),
    "MpPolicy.cs": ("GLUE", "Multiplayer host policy: FCS only if the host runs it and allows it (HostAllowFcs), host-locked variant (HostLockVariant)."),
    "FcsApi.cs": ("GLUE", "Public discovery API for HUD / visualizer tools (ApiId duythinh2022.fcs)."),
    "Telemetry.cs": ("GLUE", "CSV flight log writer (BepInEx/<Jet>_FCS_<date>_<time>.csv); since 3.0.4 the rows go to a background writer thread."),
}


def norm(path, ns):
    if not os.path.exists(path):
        return None
    t = open(path, encoding="utf-8-sig").read().replace(ns, "NS")
    return t.splitlines()


def changed(a, b):
    if a is None or b is None:
        return "—"
    n = sum(1 for l in difflib.unified_diff(a, b, lineterm="", n=0) if (l.startswith("+") or l.startswith("-")) and not l.startswith(("+++", "---")))
    return "identical" if n == 0 else str(n)


def main():
    files = sorted({f for _, rel, _ in JETS for f in os.listdir(os.path.join(ROOT, rel)) if f.endswith(".cs")})
    rows = []
    for f in files:
        src = [norm(os.path.join(ROOT, rel, f), ns) for _, rel, ns in JETS]
        lens = [str(len(s)) if s is not None else "—" for s in src]
        diffs = [changed(src[k - 1], src[k]) for k in range(1, len(JETS))]
        layer, role = ROLE.get(f, ("?", ""))
        rows.append((layer, f, lens, diffs, role))
    order = ["LAW", "MODEL", "AERO", "SYSTEM", "GLUE", "EXTRA", "?"]
    rows.sort(key=lambda r: (order.index(r[0]), r[1]))
    names = [j for j, _, _ in JETS]
    L = ["# Module map (generated)", "",
         "Generated by `tools/gen_module_map.py`. Do not edit by hand.", "",
         "Layers: **LAW** the control laws, allocation and estimation · **MODEL** what the FCS believes about the airframe ·",
         "**AERO** aerodynamic changes made to the game airframe (the FCS models mirror them) · **SYSTEM** aircraft systems the FCS",
         "also runs · **GLUE** game integration · **EXTRA** gameplay / cosmetic extras, not needed for flight control.", "",
         "## Files, roles and size", "",
         "Line count of each file on each jet (— = the file does not exist on that jet).", "",
         "| Layer | File | " + " | ".join(names) + " | Role |", "|---|---|" + "---|" * len(names) + "---|"]
    for layer, f, lens, diffs, role in rows:
        L.append(f"| {layer} | `{f}` | " + " | ".join(lens) + f" | {role} |")
    L += ["", "## How far each jet has moved from the one it was forked from", "",
          "The jets were forked in a chain: F-22E → Ifrit → Revoker → Compass → Ternion → Su-30. Each column counts the changed lines",
          "between a jet and its parent, namespace normalised (`identical` = the same file). A port of your own starts from the jet",
          "whose column is smallest for the files you care about.", "",
          "| Layer | File | " + " | ".join(f"{names[k-1]}→{names[k]}" for k in range(1, len(names))) + " |",
          "|---|---|" + "---|" * (len(names) - 1)]
    for layer, f, lens, diffs, role in rows:
        L.append(f"| {layer} | `{f}` | " + " | ".join(diffs) + " |")
    out = os.path.join(ROOT, "docs", "MODULE_MAP.md")
    open(out, "w").write("\n".join(L) + "\n")
    print(f"wrote {out}: {len(rows)} files")


if __name__ == "__main__":
    main()
