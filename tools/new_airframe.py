#!/usr/bin/env python3
"""Start an FCS for a new aircraft by cloning one of the reference trees under a new identity.

usage:
  python3 tools/new_airframe.py --from f22e|ifrit|revoker|compass|ternion|su30 --out <dir> --ns <Namespace.FCS> \
      --title "<display name>" --short "<log name>" --file-prefix <CsvPrefix> [--prefab <GameObjectNamePrefix>] \
      --match "<name part>" [--match ...] [--harmony <harmony id>] [--version 0.1.0]

example (a hypothetical "Aurora" jet whose prefab is called Fighter7 and whose AircraftParameters.aircraftName is "AF-7 Aurora"):
  python3 tools/new_airframe.py --from revoker --out ~/Aurora-FCS --ns Aurora_AF7.FCS --title "AF-7 Aurora FCS" \
      --short "Aurora FCS" --file-prefix Aurora --prefab Fighter7 --match "AF-7" --match "Aurora"

What it changes (mechanically, nothing else):
  - the plugin folder and C# namespace (plugin, DevUnlock), the BepInEx GUID / Dev GUID / builtin-defaults file name,
    the Harmony id, the plugin display name, FcsApi.FcsName (if present), the version string;
  - the log prefix ("Revoker FCS ..." -> "<short> ...") and the flight-log file prefix (Revoker_FCS_*.csv -> <prefix>_FCS_*.csv);
  - the aircraft match in Registry.IsF22 (prefab-name prefix, if given, and aircraftName parts) - the method keeps its
    historical name. Without --prefab the jet is matched by aircraftName only (what the Su-30 does);
  - the bench sync script and build wrappers, so `bash bench.sh build` compiles the new tree against the bench stubs;
  - the native (Rust) core's library name (<Old>_FcsCore -> <FilePrefix>_FcsCore): the C# DllImport name, the resource
    build.sh embeds, native/prebuilt/<Lib>.dll, Cargo's [lib] name and the bench copy step. Windows binds a DllImport by
    module name, so two plugins carrying cores of the same name would share whichever loaded first; renaming keeps a clone
    and its source jet independent in one game. The prebuilt core is the source jet's (same exports, same ABI), which is
    correct until you change the Rust code; then rebuild it with native/build.sh.
It does NOT change any control law, effector setup, limit or tuning value. It writes PORT_TODO.md into the clone: the list of
places that are specific to the source jet and that you must review for your aircraft (README section 7).
"""
import argparse, os, re, shutil, sys

KIT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
BENCH_NS = "Aryx_F22E_StrikeRaptor.FCS"   # every bench compiles the synced FCS copy in this namespace (its stubs live there)

SRC = {
    # fork order: f22e -> ifrit -> revoker -> compass -> ternion -> su30 (each forked from the one before it)
    # lib = the native (Rust) core's library name: the C# DllImport name, the embedded resource and native/prebuilt/<lib>.dll
    "f22e": dict(dir="reference/F22E-FCS-4.3.0", ns="Aryx_F22E_StrikeRaptor.FCS", title="F-22E Strike Raptor FCS", short="F-22E FCS",
                 harmony="com.aryx.strikeraptor.fcs", csv="F22E_FCS_", sync="bench/sync.sh", lib="F22E_FcsCore"),
    "ifrit": dict(dir="reference/Ifrit-FCS-1.3.0", ns="Ifrit_KR67.FCS", title="KR-67 Ifrit FCS", short="Ifrit FCS",
                  harmony="tom.ifrit_kr67.fcs", csv="Ifrit_FCS_", sync="bench/isync.sh", lib="Ifrit_FcsCore"),
    "revoker": dict(dir="reference/Revoker-FCS-1.4.1", ns="Revoker_FS12.FCS", title="FS-12 Revoker FCS", short="Revoker FCS",
                    harmony="tom.revoker_fs12.fcs", csv="Revoker_FCS_", sync="bench/rsync.sh", lib="Revoker_FcsCore"),
    "compass": dict(dir="reference/Compass-FCS-0.5.0", ns="Compass_TA30.FCS", title="T/A-30 Compass FCS", short="Compass FCS",
                    harmony="tom.compass_ta30.fcs", csv="Compass_FCS_", sync="bench/rsync.sh", lib="Compass_FcsCore"),
    "ternion": dict(dir="reference/Ternion-FCS-0.4.0", ns="Ternion_FS3.FCS", title="FS-3 Ternion FCS", short="Ternion FCS",
                    harmony="tom.ternion_fs3.fcs", csv="Ternion_FCS_", sync="bench/rsync.sh", lib="Ternion_FcsCore"),
    "su30": dict(dir="reference/Su30-FCS-0.6.0", ns="Su30_VXN.FCS", title="Su-30VXN FCS", short="Su-30 FCS",
                 harmony="tom.su30_vxn.fcs", csv="Su30_FCS_", sync="bench/rsync.sh", lib="Su30_FcsCore"),
}

SKIP_DIRS = {"bin", "obj", "out", "fcs", "release-notes", "__pycache__", "target", "studies"}

# places that are specific to the source jet: (file glob relative to the plugin folder or tree, regex, what to check)
TODO = [
    ("{P}/Registry.cs", r"public static bool IsF22", "Aircraft match (done by this tool) - confirm it matches only your aircraft (prefab name / aircraftName)."),
    ("{P}/Registry.cs", r"bool flag = num2 >= \d+ && num >= \d+", "Setup completeness check: minimum surfaces / engines before the airframe setup counts as done."),
    ("{P}/Registry.cs", r"Plugin\.(ThrustScale|DragScale|ActuatorScale|SurfaceServoSpeed|TvcRange|SplitRange|LerxEffectiveness)", "Airframe setup: performance-profile and actuator changes applied to the game aircraft. Remove what you do not want."),
    ("{P}/FcsController.cs", r"private static SurfaceKind Classify", "Surface roles from GameObject names / ranges. Map YOUR surface names to Stabilator / Flaperon / Aileron / Rudder."),
    ("{P}/FcsController.cs", r"expected \d+ control surfaces", "Minimum surface count in the constructor."),
    ("{P}/FcsController.cs", r"public FcsController\(Aircraft aircraft\)", "Effector list: surfaces, pairing (_L/_R), TVC, splits, differential/yaw TVC, hinged surfaces."),
    ("{P}/FcsController.cs", r"Replace\(\"Aryx_KingRaptor_\"", "Part-name prefix stripping for the camber host lookup."),
    ("{P}/Camber.cs", r"HostOf|KForName", "Camber coupling: which fixed part hosts each surface, by name, and its K. Rewrite for your part names or disable."),
    ("{P}/StallStagger.cs", r"FamilyOf|Fam\.", "Staggered stall families by surface name. Rewrite or disable (Enabled) for your aircraft."),
    ("{P}/Plugin.cs", r"\"AoALimitPositive\"|\"AoALimitNegative\"|\"MaxPitchRate\"|\"GLimitPositive\"|\"MaxYawRate\"", "Envelope defaults: set them for your airframe."),
    ("{P}/Plugin.cs", r"\"RollStopSideslip\"|\"RollPaceRateBlend\"|\"PullRollTrade\"|\"PushoverSideslip\"", "Lateral-governor defaults (player file)."),
    ("{P}/CapacityEstimator.cs", r"internal static int Group\(|GainYaw|GainRoll", "Estimator effector groups and per-group gains - every lateral effector must be in a group."),
    ("{P}/Hooks.cs", r"TurbojetTranspiler|TvcPitch|TvcRoll|TvcYaw", "Engine hooks: how nozzle commands reach the game's engine (Turbojet vs Turbofan)."),
    ("{P}/Plugin.cs", r"\"PerformanceProfile\"|\"FlareCountScale\"|\"GunAmmoScale\"", "Gameplay extras (performance profile, countermeasures). Optional."),
    ("{P}/Plugin.cs", r"DevRevision|ConfigRevision|AirframeRevision|GovernorRevision", "One-time config migrations (DevRevision / AirframeRevision ...): they rewrite the source jet's old defaults in existing config files. Reset or delete them for a new jet."),
    ("{P}/Names.cs", r"Canon", "Su-30 lineage: blueprint part names mapped onto the _L/_R scheme. Rewrite for your part names."),
    ("{P}/StructureFix.cs", r"engine|Engine", "Ternion lineage: re-homing of parts whose transform parent is an engine. Check your part hierarchy; delete if not needed."),
    ("{P}/ThrustLine.cs", r"ThrustLineAtCg", "Su-30 lineage: thrust force point moved to the CG height. Only if your nozzles sit well off the CG."),
    ("{P}/CantedTvc.cs", r"cant|Cant", "Su-30 lineage: canted single-axis nozzles. Set the cant angle / travel or remove for your engines."),
    ("{P}/Strake.cs", r"wingroot1|Strake", "Compass lineage: strake / LERX vortex-lift curve keyed by part name. Rewrite or disable."),
    ("{P}/RaptorFoil.cs", r"RaptorStabFoil", "Su-30 lineage: the F-22E stab curve put on the stabs. Keep only if you want it."),
    ("{P}/Registry.cs", r"FuelConsumptionScale|UseFuel", "Fuel hook / buffed fuel burn: keep the two-prefix form (scale at Priority.First, draw at Priority.Last) - see README failure modes."),
    ("bench/sim/Airframe.cs", r"gameObject\.name ==|ifrit =|revoker =|su30|ternion|compass", "Bench airframe flags keyed to the source jet's root name."),
    ("bench.sh", r"BENCH_DATA", "Bench airframe JSON path: point it at YOUR extracted airframe (README section 9)."),
    ("{P}/FcsNative.cs", r"public const string Lib|public const int Abi|ABI", "Native core: library name (renamed by this tool) and ABI number. Any change to the allocator, the grid kernels or the coupled-check LP must be made in native/fcs_core/src as well, expression for expression, with an ABI bump (README section 2.18)."),
    ("{P}/CoupledCap.cs", r"Reserve|FindTop|CplBetaDesign", "Coupled asymmetric-load check: the reserve share (AsymCoupledReserveMax) is per jet; check the demand curve's shape before choosing the edge rule (README section 8)."),
    ("{P}/FcsController.cs", r"ReversedFlowNoseAxis|LatAxisAlpha", "Reversed-flow (flying backwards) roll axis: |cos AoA| mirror. Keep."),
    ("{P}/Hooks.cs", r"GearPrefix|GroundIndiBrake|GsPedalBrake", "Ground steering with the main brakes: read YOUR gear's braking, differentialBrakeFactor and track (README section 8)."),
]


def copy_tree(src, dst):
    def ign(d, names):
        return [n for n in names if n in SKIP_DIRS]
    shutil.copytree(src, dst, ignore=ign)


def sub_file(path, pairs, regex_pairs=()):
    s = open(path, encoding="utf-8-sig").read()
    o = s
    for a, b in pairs:
        s = s.replace(a, b)
    for a, b in regex_pairs:
        s = re.sub(a, b, s)
    if s != o:
        open(path, "w", encoding="utf-8").write(s)
        return True
    return False


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--from", dest="src", required=True, choices=SRC.keys())
    ap.add_argument("--out", required=True)
    ap.add_argument("--ns", required=True, help="new namespace = plugin GUID, e.g. Aurora_AF7.FCS")
    ap.add_argument("--title", required=True, help='display name, e.g. "AF-7 Aurora FCS"')
    ap.add_argument("--short", required=True, help='log prefix, e.g. "Aurora FCS"')
    ap.add_argument("--file-prefix", required=True, help="flight-log file prefix, e.g. Aurora -> Aurora_FCS_<date>.csv")
    ap.add_argument("--prefab", default="", help="the aircraft GameObject name prefix (prefab); omit to match by aircraftName only")
    ap.add_argument("--match", action="append", default=[], help="part of AircraftParameters.aircraftName (repeatable)")
    ap.add_argument("--harmony", default=None)
    ap.add_argument("--version", default="0.1.0")
    a = ap.parse_args()
    if not re.match(r"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$", a.ns):
        sys.exit("--ns must be a valid C# namespace")
    if not a.match:
        sys.exit("give at least one --match")
    S = SRC[a.src]
    src = os.path.join(KIT, S["dir"])
    dst = os.path.abspath(os.path.expanduser(a.out))
    if os.path.exists(dst):
        sys.exit(f"{dst} exists")
    copy_tree(src, dst)
    os.rename(os.path.join(dst, S["ns"]), os.path.join(dst, a.ns))
    P = os.path.join(dst, a.ns)
    harmony = a.harmony or ("fcs." + a.ns.lower())
    vrx = (r'public const string Version = "[^"]*";', f'public const string Version = "{a.version}";')
    pairs = [(S["harmony"], harmony), (S["title"], a.title), (S["csv"], a.file_prefix + "_FCS_"), (S["ns"], a.ns), (S["short"], a.short)]
    n = 0
    for root in (P, os.path.join(dst, "DevUnlock"), os.path.join(dst, "Properties")):
        if not os.path.isdir(root):
            continue
        for f in os.listdir(root):
            if f.endswith(".cs"):
                n += sub_file(os.path.join(root, f), pairs, [vrx])
    # native core library name: every text file that names it, and the prebuilt binary
    new_lib = re.sub(r"[^A-Za-z0-9_]", "_", a.file_prefix) + "_FcsCore"
    if S.get("lib") and new_lib != S["lib"]:
        for root, dirs, files in os.walk(dst):
            for f in files:
                if f.endswith((".cs", ".sh", ".toml", ".cfg", ".py", ".md")):
                    sub_file(os.path.join(root, f), [(S["lib"], new_lib)])
        pb = os.path.join(dst, "native", "prebuilt")
        if os.path.exists(os.path.join(pb, S["lib"] + ".dll")):
            os.rename(os.path.join(pb, S["lib"] + ".dll"), os.path.join(pb, new_lib + ".dll"))
    # aircraft match: only inside Registry.IsF22
    reg = os.path.join(P, "Registry.cs")
    s = open(reg, encoding="utf-8-sig").read()
    i = s.index("public static bool IsF22")
    j = s.index("isF22[instanceID] = value;", i)
    body = s[i:j]
    # the prefab clause: the first `value = ...;` in IsF22 (StartsWith on the F-22E / Ifrit / Revoker, exact names on the
    # Compass / Ternion, `false` on the Su-30)
    pre = (f'value = ((Component)(object)a).gameObject.name.StartsWith("{a.prefab}", StringComparison.Ordinal);' if a.prefab
           else "value = false;")
    body, k0 = re.subn(r'\bvalue = (?!aircraftParameters)[^;]*;', lambda m: pre, body, count=1)
    if k0 != 1:
        sys.exit("could not rewrite the prefab match in Registry.IsF22 - edit it by hand")
    cond = " || ".join(f'aircraftParameters.aircraftName.Contains("{m}")' for m in a.match)
    body, k = re.subn(r'aircraftParameters\.aircraftName != null && (\()?aircraftParameters\.aircraftName\.Contains\("[^"]*"\)( \|\| aircraftParameters\.aircraftName\.Contains\("[^"]*"\))*(\))?',
                      f'aircraftParameters.aircraftName != null && ({cond})', body)
    if k != 1:
        sys.exit("could not rewrite the aircraftName match in Registry.IsF22 - edit it by hand")
    s = s[:i] + body + s[j:]
    open(reg, "w", encoding="utf-8").write(s)
    # bench: sync script + wrappers
    sync = os.path.join(dst, S["sync"])
    t = open(sync).read()
    t = t.replace(f'S="$1/{S["ns"]}"', f'S="$1/{a.ns}"').replace(f"dir containing {S['ns']}", f"dir containing {a.ns}")
    if S["ns"] != BENCH_NS:
        t = t.replace(S["ns"].replace(".", r"\."), a.ns.replace(".", r"\.")).replace(f"namespace {S['ns']}", f"namespace {a.ns}")
    else:
        # the F-22E tree is already in the bench namespace: rename the clone's namespace back for the bench copy
        t = t.replace("sed -i -E 's/\\bprivate\\b/internal/g' fcs/FcsController.cs",
                      "sed -i -E 's/\\bprivate\\b/internal/g' fcs/FcsController.cs\n"
                      f"sed -i -E 's/namespace {a.ns.replace('.', chr(92) + '.')}/namespace {BENCH_NS}/' fcs/*.cs")
        t = t.replace("s=open(sys.argv[1]).read();", f"s=open(sys.argv[1]).read().replace('namespace {a.ns}','namespace {BENCH_NS}');")
    open(sync, "w").write(t)
    for w in ("bench.sh", "regen.sh"):
        p = os.path.join(dst, w)
        if os.path.exists(p):
            sub_file(p, [(f"../{S['ns']}/", f"../{a.ns}/"), (f"({S['ns']})", f"({a.ns})")])
    # PORT_TODO.md
    L = [f"# Port checklist for {a.title}", "",
         f"Cloned from `{S['dir']}` by `tools/new_airframe.py`. Identity, aircraft match and bench wiring are done.",
         "Everything below is still the source jet's and must be reviewed for your aircraft. Work through it with README section 7",
         "(porting procedure), section 8 (solution library) and section 9 (models). Line numbers are as cloned.", ""]
    for fg, rx, what in TODO:
        path = os.path.join(dst, fg.replace("{P}", a.ns))
        if not os.path.exists(path):
            continue
        hits = [(k + 1, l.strip()) for k, l in enumerate(open(path, encoding="utf-8-sig", errors="replace")) if re.search(rx, l)]
        rel = os.path.relpath(path, dst)
        L.append(f"- [ ] **{what}**")
        for ln, txt in hits[:8]:
            L.append(f"  - `{rel}:{ln}` `{txt[:140]}`")
        if len(hits) > 8:
            L.append(f"  - ... {len(hits) - 8} more in `{rel}`")
    open(os.path.join(dst, "PORT_TODO.md"), "w").write("\n".join(L) + "\n")
    print(f"cloned {a.src} -> {dst}\n  namespace {a.ns}, title '{a.title}', log '{a.short}', csv {a.file_prefix}_FCS_*.csv, harmony {harmony}, v{a.version}")
    print(f"  aircraft match: {('prefab ' + repr(a.prefab) + '* or ') if a.prefab else ''}aircraftName contains {a.match}\n  {n} C# files rewritten; see {os.path.join(dst, 'PORT_TODO.md')}")
    if S.get("lib"):
        print(f"  native core: {S['lib']} -> {new_lib} (prebuilt binary renamed; rebuild with native/build.sh after changing the Rust code)")


if __name__ == "__main__":
    main()
