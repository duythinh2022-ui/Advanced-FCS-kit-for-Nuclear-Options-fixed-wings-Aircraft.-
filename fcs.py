#!/usr/bin/env python3
"""fcs.py - one front door to the NO FCS kit (kit 5.0).

Start here:
    python fcs.py                  what you can do, in plain words
    python fcs.py doctor           checks your tools and tells you exactly what is missing
    python fcs.py list             the eight reference jets and which one to start a port from

Build a plugin (works on Windows, Linux and macOS; no bash needed):
    python fcs.py setup --game "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Nuclear Option"
    python fcs.py build revoker            -> out/Revoker-1.8.6/: main DLL + native core DLL + ready-to-upload zips
    python fcs.py build revoker --unified  also the single-file DLL (core inside), as an optional download

Fly the bench and read the result:
    python fcs.py bench revoker build      compile the offline simulator (needs bash: Git Bash or WSL on Windows)
    python fcs.py fly revoker pull-roll    a ready-made manoeuvre; prints a plain-English readout and curve checks
    python fcs.py fly revoker pull-roll ias=550 MaxPitchRate=48     change the entry speed or any FCS setting
    python fcs.py presets                  the manoeuvres you can fly
    python fcs.py replay revoker my_flight.csv 120 135              your flight log, from its logged state
    python fcs.py summary some_run.csv [--plot]                     read any bench or flight CSV
    python fcs.py suite revoker before  /  ... suite revoker after  /  python fcs.py compare revoker before after

Port to a new aircraft:
    python fcs.py new                      asks a few questions, then clones the closest reference jet
    python fcs.py check                    are the files every jet shares still identical

Run `python fcs.py <command> -h` for the options of one command. Every command says what it did and where the files are.
"""
import argparse, csv, glob, hashlib, json, math, os, re, shutil, subprocess, sys, tempfile, time, zipfile

KIT = os.path.dirname(os.path.abspath(__file__))
REF = os.path.join(KIT, "reference")
LOCAL = os.path.join(KIT, "fcs.local.json")      # your machine's settings (game folder, airframe data folders); not in git

# ---------------------------------------------------------------------------------------------------------------------------
# the eight reference jets. key -> facts the commands need. `zip` = the release's player-zip name stem.
JETS = {
    "f22e":    dict(folder="F22E", plugin="Aryx_F22E_StrikeRaptor.FCS", asm="Aryx_F22E_StrikeRaptor_FCS", zip="F22E-StrikeRaptor-FCS",
                    name="F-22E Strike Raptor (Aryx's mod)", bench="one", data=["bench/data/airframe_cal.json"], dmg="bench/data/f22e_damage.json",
                    owner="mod", start="rudders + stabilators + flaperons, pitch-only TVC, twin engine; the original line"),
    "ifrit":   dict(folder="Ifrit", plugin="Ifrit_KR67.FCS", asm="Ifrit_KR67_FCS", zip="Ifrit-KR67-FCS",
                    name="KR-67 Ifrit (stock)", bench="ifly", data=["bench/extract/ifrit_airframe.json"], dmg="bench/data/ifrit_damage.json",
                    owner="game", prefab="Multirole1", start="canted tails (pitch + yaw, no rudders), split decelerons, pitch + roll TVC"),
    "revoker": dict(folder="Revoker", plugin="Revoker_FS12.FCS", asm="Revoker_FS12_FCS", zip="FS12-Revoker-FCS",
                    name="FS-12 Revoker (stock)", bench="ifly", data=["bench/extract/rev_airframe.json"], dmg="bench/data/revoker_damage.json",
                    owner="game", prefab="Fighter1", start="canards, one rudder, delta elevons, pitch + yaw TVC, single engine; the safe default"),
    "compass": dict(folder="Compass", plugin="Compass_TA30.FCS", asm="Compass_TA30_FCS", zip="TA30-Compass-FCS",
                    name="T/A-30 Compass (stock trainer)", bench="ifly", data=["bench/extract/compass_airframe.json"], dmg="bench/data/compass_damage.json",
                    owner="game", start="conventional tail, strakes, ailerons + flaps, no TVC, twin engine"),
    "ternion": dict(folder="Ternion", plugin="Ternion_FS3.FCS", asm="Ternion_FS3_FCS", zip="FS3-Ternion-FCS",
                    name="FS-3 Ternion (Blueprinter)", bench="ifly", data=["bench/extract/ternion_airframe.json"], dmg="bench/data/ternion_damage.json",
                    owner="blueprint", start="canards AND aft elevators, twin rudders, no TVC, engine-out zero-thrust match"),
    "su30":    dict(folder="Su30", plugin="Su30_VXN.FCS", asm="Su30_VXN_FCS", zip="Flanker-Su30-VXN-FCS",
                    name="Su-30VXN Gandiva (kyleisred's mod)", bench="ifly", data=["bench/extract/su30_airframe.json"], dmg="bench/data/ternion_damage.json",
                    owner="mod", start="canards + stabs + rudders, canted single-axis nozzles, thrust line off the CG"),
    "vortex":  dict(folder="Vortex", plugin="Vortex_FS20.FCS", asm="Vortex_FS20_FCS", zip="FS20-Vortex-FCS",
                    name="FS-20 Vortex (stock, STOVL)", bench="ifly", data=["bench/extract/vortex_airframe.json", "bench/extract/vortex_systems.json"],
                    dmg="bench/data/revoker_damage.json", owner="game", prefab="VTOL4", stovl=True,
                    start="STOVL: lift fan, roll posts, swivel nozzle; delta with two elevon pairs"),
    "j20b":    dict(folder="J20B", plugin="J20B_Weilong.FCS", asm="J20B_Weilong_FCS", zip="J20B-Weilong-FCS",
                    name="J-20B Weilong (Reasonix's mod)", bench="ifly", data=["bench/extract/j20_airframe.json"], dmg=None, owner="mod",
                    start="canards + all-moving fins, hooks the mod's own surface system (Reasonix.J20.AeroControl)"),
}
ALIASES = {"f22": "f22e", "raptor": "f22e", "kr67": "ifrit", "fs12": "revoker", "ta30": "compass", "fs3": "ternion",
           "su-30": "su30", "flanker": "su30", "fs20": "vortex", "j20": "j20b", "j-20b": "j20b", "weilong": "j20b"}

# manoeuvres: stick profiles as "time:value" points, straight lines between them (time in s from the start; +1 = full aft /
# full right). T = run length. Every one can take ias=, alt=, thr= (0..1) and any FCS setting Name=value.
PRESETS = {
    "pull":      dict(T=6, pitch="0:0,0.2:1,6:1", roll="0:0", what="full aft stick, held 6 s: AoA / g capture and the limit"),
    "roll":      dict(T=5, pitch="0:0", roll="0:0,0.1:1,3:1,3.1:0", what="full roll right at 1 g for 3 s, then let go: roll onset, rate and the stop"),
    "pull-roll": dict(T=6, pitch="0:0,0.2:1,6:1", roll="0:0,0.2:1,6:1", what="full aft + full roll right, held: the loaded roll at the AoA limit"),
    "reversal":  dict(T=7, pitch="0:0,0.2:1,7:1", roll="0:0,0.2:1,3:1,3.1:-1,6:-1,6.1:0", what="pulled, roll right 3 s, snap to full left 3 s, let go"),
    "stop":      dict(T=6, pitch="0:0,0.2:0.6,6:0.6", roll="0:0,0.2:1,2:1,2.1:0", what="0.6 aft, full roll 2 s then let go: the roll stop and sideslip after it"),
    "pushover":  dict(T=7, pitch="0:0,0.2:1,2:1,2.2:-1,7:-1", roll="0:0,2.5:0,2.6:1,7:1", what="full aft 2 s, full forward, roll right from 2.5 s"),
    "corkscrew": dict(T=8, pitch="0:0,0.2:1,8:1", roll="0:0,0.2:1,8:1", yaw="0:0,0.2:1,8:1", what="full aft + full roll + full pedal, held 8 s"),
    "hover":     dict(stovl=True, what="Vortex only: conversion from 250 km/h to a hover at 300 m, 60 s (the kit's STOVL check case)"),
}


def die(msg, hint=None):
    print("\n  Problem: " + msg)
    if hint:
        print("  What to do: " + hint)
    print()
    sys.exit(1)


def ok(msg):
    print("  " + msg)


def jet_key(s):
    k = s.lower().replace("_", "").replace(" ", "")
    k = ALIASES.get(k, k)
    if k not in JETS:
        die(f"'{s}' is not one of the kit's jets.", "pick one of: " + ", ".join(JETS) + "  (python fcs.py list)")
    return k


def tree(k):
    hits = sorted(glob.glob(os.path.join(REF, JETS[k]["folder"] + "-FCS-*")))
    if not hits:
        die(f"no reference/{JETS[k]['folder']}-FCS-* folder in the kit.")
    return hits[-1]


def version(k):
    return os.path.basename(tree(k)).split("-FCS-")[1]


def load_local():
    try:
        return json.load(open(LOCAL))
    except Exception:
        return {}


def save_local(d):
    json.dump(d, open(LOCAL, "w"), indent=2)


def sha(p, n=None):
    h = hashlib.sha256(open(p, "rb").read()).hexdigest()
    return h[:n] if n else h


def human(n):
    return f"{n/1048576:.2f} MB" if n > 1048576 else f"{n/1024:.0f} KB"

# ---------------------------------------------------------------------------------------------------------------------------
# tools on this machine

def find_game():
    env = os.environ.get("NO_GAME")
    cands = [env] if env else []
    cands.append(load_local().get("game"))
    home = os.path.expanduser("~")
    cands += [r"C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option", r"D:\SteamLibrary\steamapps\common\Nuclear Option",
              os.path.join(home, ".steam/steam/steamapps/common/Nuclear Option"),
              os.path.join(home, ".local/share/Steam/steamapps/common/Nuclear Option"),
              os.path.join(home, "Library/Application Support/Steam/steamapps/common/Nuclear Option"),
              "/home/claude/game/Nuclear Option"]
    for c in cands:
        if c and os.path.isfile(os.path.join(c, "NuclearOption_Data", "Managed", "Assembly-CSharp.dll")):
            return c
    return None


def game_dirs():
    g = find_game()
    m = os.environ.get("NO_MANAGED") or (g and os.path.join(g, "NuclearOption_Data", "Managed"))
    b = os.environ.get("NO_BEPINEX_CORE") or (g and os.path.join(g, "BepInEx", "core"))
    return m, b


def find_dotnet():
    return shutil.which("dotnet")


def find_csc():
    if os.environ.get("CSC") and os.path.isfile(os.environ["CSC"]):
        return os.environ["CSC"]
    dn = find_dotnet()
    roots = []
    if dn:
        try:
            for line in subprocess.run([dn, "--list-sdks"], capture_output=True, text=True).stdout.splitlines():
                m = re.match(r"(\S+) \[(.+)\]", line.strip())
                if m:
                    roots.append(os.path.join(m.group(2), m.group(1)))
        except Exception:
            pass
    roots += glob.glob("/usr/lib/dotnet/sdk/*") + glob.glob("/usr/share/dotnet/sdk/*") + glob.glob(r"C:\Program Files\dotnet\sdk\*")
    # prefer an 8.0 SDK: the releases were compiled with 8.0.134's Roslyn, which is what the byte-for-byte check expects
    roots.sort(key=lambda r: (not os.path.basename(r).startswith("8.0"), r))
    for r in roots:
        p = os.path.join(r, "Roslyn", "bincore", "csc.dll")
        if os.path.isfile(p):
            return p
    return None


def find_bash():
    b = shutil.which("bash")
    if b and sys.platform == "win32" and b.lower().endswith(r"system32\bash.exe"):
        return b  # WSL's bash; works, paths are translated by WSL
    if b:
        return b
    for c in (r"C:\Program Files\Git\bin\bash.exe", r"C:\Program Files (x86)\Git\bin\bash.exe"):
        if os.path.isfile(c):
            return c
    return None


def cmd_doctor(a):
    print("\nChecking what this machine has for the kit:\n")
    rows = []
    py = sys.version.split()[0]
    rows.append(("Python 3.8+", sys.version_info >= (3, 8), py, "install Python 3 from python.org"))
    dn = find_dotnet()
    rows.append((".NET 8 SDK (dotnet)", bool(dn), dn or "not found", "install the .NET 8 SDK from dot.net (needed to build plugins and the bench)"))
    csc = find_csc()
    rows.append(("C# compiler (csc.dll)", bool(csc), csc or "not found", "comes with the .NET SDK"))
    g = find_game()
    rows.append(("Nuclear Option folder", bool(g), g or "not found",
                 'python fcs.py setup --game "<your Nuclear Option folder>"  (or set NO_GAME). Needed only to build plugin DLLs'))
    m, b = game_dirs()
    rows.append(("BepInEx 5 in the game", bool(b and os.path.isfile(os.path.join(b, "BepInEx.dll"))), b or "-", "install BepInEx 5 into the game"))
    bash = find_bash()
    rows.append(("bash (bench build / suites)", bool(bash), bash or "not found", "Windows: install Git for Windows (Git Bash) or WSL. Not needed for build / fly / summary"))
    cargo = shutil.which("cargo")
    rows.append(("Rust (optional)", bool(cargo), cargo or "not found", "only to rebuild a native core; the kit ships prebuilt cores, and the bench runs C# without it"))
    try:
        import pandas  # noqa
        pd_ok = True
    except Exception:
        pd_ok = False
    rows.append(("pandas (optional)", pd_ok, "yes" if pd_ok else "no", "pip install pandas numpy  (the suites' analysis scripts use it)"))
    try:
        import matplotlib  # noqa
        mp = True
    except Exception:
        mp = False
    rows.append(("matplotlib (optional)", mp, "yes" if mp else "no", "pip install matplotlib  (for `summary --plot`)"))
    for name, good, val, fix in rows:
        print(f"  [{'ok' if good else '--'}] {name:30s} {val}")
        if not good:
            print(f"        -> {fix}")
    print("\nAirframe data for the bench (never in the kit, you make it from your own game / mod copy):")
    for k in JETS:
        st, where = data_status(k)
        print(f"  [{'ok' if st else '--'}] {k:8s} {where}")
    print("\n  `python fcs.py data <jet>` explains how to make the missing ones.\n")


def cmd_setup(a):
    d = load_local()
    if a.game:
        if not os.path.isfile(os.path.join(a.game, "NuclearOption_Data", "Managed", "Assembly-CSharp.dll")):
            die(f"'{a.game}' does not look like the Nuclear Option folder (no NuclearOption_Data/Managed/Assembly-CSharp.dll).",
                "point at the folder that holds NuclearOption.exe. In Steam: right-click the game > Manage > Browse local files.")
        d["game"] = os.path.abspath(a.game)
        ok(f"game folder saved: {d['game']}")
    save_local(d)
    ok(f"settings are in {os.path.relpath(LOCAL, KIT)} (only on this machine)")


def cmd_list(a):
    print("\nThe kit's reference jets (newest release of each). Clone the one closest to your aircraft:\n")
    for k, j in JETS.items():
        st, _ = data_status(k)
        bench = "bench, airframe data " + ("found" if st else "missing")
        print(f"  {k:8s} {version(k):7s} {j['name']}")
        print(f"           {j['start']}")
        print(f"           {bench}")
    print("\n  Unsure? Start from revoker. STOVL / lift system: vortex. Canted nozzles: su30. No TVC: compass or ternion.\n")

# ---------------------------------------------------------------------------------------------------------------------------
# building the plugin: the same compile as each tree's build.sh, in Python so it runs on Windows too

def lib_name(t, plugin):
    s = open(os.path.join(t, plugin, "FcsNative.cs"), encoding="utf-8").read()
    m = re.search(r'public const string Lib = "([A-Za-z0-9_]+)"', s)
    return m.group(1) if m else None


def refs_from_build_sh(t):
    s = open(os.path.join(t, "build.sh"), encoding="utf-8").read()
    return re.findall(r'-r:"\$([MB])/([^"]+)"', s)


def sources(t, plugin, k):
    """the C# files build.sh compiles, in build.sh's order (C-locale sort of the path)"""
    skip = {"bench", "DevUnlock", "tools", "obj", "bin"}
    if k == "ternion":
        skip.add("native")
    out = []
    base = os.path.join(t, plugin)
    for dp, dn, fn in os.walk(base):
        dn[:] = [d for d in dn if d not in skip]
        for f in fn:
            if f.endswith(".cs"):
                out.append(os.path.relpath(os.path.join(dp, f), t).replace(os.sep, "/"))
    return sorted(out, key=lambda p: p.encode())


def compile_dll(k, out_dll, unified, defines=""):
    j = JETS[k]; t = tree(k)
    csc = find_csc(); dn = find_dotnet()
    if not (csc and dn):
        die("no .NET SDK / C# compiler found.", "install the .NET 8 SDK from dot.net, then run `python fcs.py doctor`.")
    m, b = game_dirs()
    if not (m and os.path.isfile(os.path.join(m, "Assembly-CSharp.dll"))):
        die("the game's DLLs were not found.", 'python fcs.py setup --game "<your Nuclear Option folder>"   (or set NO_GAME)')
    if not os.path.isfile(os.path.join(b, "BepInEx.dll")):
        die(f"BepInEx.dll is not in {b}.", "install BepInEx 5 into the game (or set NO_BEPINEX_CORE).")
    if k == "vortex":   # the family fingerprint file is generated from the tree, as build.sh does
        subprocess.run([sys.executable, os.path.join(t, "tools", "gen_buildinfo.py"), j["plugin"], os.path.join("native", "fcs_core")], cwd=t, check=True,
                       stdout=subprocess.DEVNULL)
    lib = lib_name(t, j["plugin"])
    core = os.path.join(t, "native", "prebuilt", f"{lib}.dll") if lib else None
    args = [dn, csc, "-nologo", "-noconfig", "-nostdlib+", "-target:library", "-langversion:9.0", "-unsafe+", "-optimize+", "-deterministic",
            "-nowarn:CS0436,CS1701,CS1702"]
    if defines:
        args += defines.split()
    if unified and core and os.path.isfile(core):
        args.append(f"-resource:{core},{lib}.dll")
    args.append(f"-out:{out_dll}")
    for which, name in refs_from_build_sh(t):
        args.append(f"-r:{os.path.join(m if which == 'M' else b, name)}")
    args += sources(t, j["plugin"], k)
    r = subprocess.run(args, cwd=t, capture_output=True, text=True)
    if r.returncode:
        print(r.stdout[-3000:], r.stderr[-2000:])
        die("the C# compile failed (messages above).", "if you changed the source, fix the first error listed; otherwise run `python fcs.py doctor`.")
    return lib, core


def compile_dev(k, out_dll):
    t = tree(k); m, b = game_dirs()
    src = os.path.join(t, "DevUnlock", "FcsDevPlugin.cs")
    if not os.path.isfile(src):
        return False
    args = [find_dotnet(), find_csc(), "-nologo", "-noconfig", "-nostdlib+", "-target:library", "-langversion:9.0", "-optimize+", "-deterministic",
            f"-out:{out_dll}"] + [f"-r:{os.path.join(m, x)}" for x in ("mscorlib.dll", "System.dll", "netstandard.dll", "UnityEngine.CoreModule.dll", "UnityEngine.dll")] + \
           [f"-r:{os.path.join(b, 'BepInEx.dll')}", src]
    return subprocess.run(args, capture_output=True, text=True).returncode == 0


CORE_README = """{name} FCS {ver} - native core (optional, recommended)

Put {lib}.dll in the same folder as {asm}.dll (BepInEx/plugins/<the FCS folder>/).
It is the FCS's Rust core: the control allocator's solve, the surface-table kernels and the asymmetric-load LP, built from
native/fcs_core in the source. The plugin checks its version and self-tests it against its own C# code when it loads.

Without this file the FCS runs its C# solver: the same control laws, more CPU per tick.
BepInEx/LogOutput.log says which one runs ("native allocator ... in use" or "... not usable, the C# solve runs").

SHA-256 of {lib}.dll: {sha}
"""


def cmd_build(a):
    k = jet_key(a.jet); j = JETS[k]; t = tree(k); ver = version(k)
    out = os.path.abspath(a.out or os.path.join(KIT, "out", f"{j['folder']}-{ver}"))
    os.makedirs(out, exist_ok=True)
    print(f"\nBuilding the {j['name']} FCS {ver} from {os.path.relpath(t, KIT)}\n")
    main = os.path.join(out, j["asm"] + ".dll")
    lib, core = compile_dll(k, main, unified=False)
    files = {"main": main}
    if core and os.path.isfile(core):
        shutil.copy2(core, os.path.join(out, lib + ".dll")); files["core"] = os.path.join(out, lib + ".dll")
    dev = os.path.join(out, j["asm"] + "_Dev.dll")
    if compile_dev(k, dev):
        files["dev"] = dev
    if a.unified:
        ud = os.path.join(out, "unified"); os.makedirs(ud, exist_ok=True)
        compile_dll(k, os.path.join(ud, j["asm"] + ".dll"), unified=True)
        files["unified"] = os.path.join(ud, j["asm"] + ".dll")
    # zips, named so the player zip sorts first on a GitHub release
    z = {}
    def mk(name, members):
        p = os.path.join(out, name)
        with zipfile.ZipFile(p, "w", zipfile.ZIP_DEFLATED) as zf:
            for arc, src in members:
                if isinstance(src, str) and os.path.isfile(src):
                    zf.write(src, arc)
                else:
                    zf.writestr(arc, src)
        z[name] = p
    meta = os.path.join(t, "meta.json")
    player = [(os.path.basename(main), main)] + ([("meta.json", meta)] if os.path.isfile(meta) else [])
    if a.core_in_main and "core" in files:
        player.append((lib + ".dll", files["core"]))
    mk(f"{j['zip']}-{ver}.zip", player)
    if "core" in files and not a.core_in_main:
        mk(f"Optional.{j['zip']}-{ver}-NativeCore.zip", [(lib + ".dll", files["core"]),
            ("README.txt", CORE_README.format(name=j["name"].split(" (")[0], ver=ver, lib=lib, asm=j["asm"], sha=sha(files["core"])))])
    if "dev" in files:
        mk(f"Optional.{j['zip']}-{ver}-DevUnlock.zip", [(os.path.basename(dev), dev)])
    if "unified" in files:
        mk(f"Optional.{j['zip']}-{ver}-Unified.zip", [(os.path.basename(files["unified"]), files["unified"])])
    print("Files (in %s):" % out)
    for role, p in files.items():
        label = {"main": "plugin DLL (C#, no core inside)", "core": "native core, ships beside the plugin", "dev": "dev/debug unlock",
                 "unified": "single-file DLL, core embedded (optional)"}[role]
        ok(f"{os.path.relpath(p, out):40s} {human(os.path.getsize(p)):>9s}  sha256 {sha(p, 16)}  {label}")
    print("\nZips to upload:")
    for n in sorted(z):
        ok(n)
    print("""
Install (player):  put the plugin DLL in BepInEx/plugins/<a folder>/ and, for the fast path, the native core DLL in the
                   SAME folder. The plugin works without the core (C# solver, more CPU).
NOMM:              list the main zip (plugin DLL only, pure C# assembly). The native core is the optional download.
                   (--core-in-main puts both files in the main zip, if a listing accepts the separate native file.)
""")
    if a.check:
        check_identity(k, a.check)


def check_identity(k, release_dll):
    """rebuild the unified DLL and compare with a shipped release DLL"""
    tmp = tempfile.mkdtemp()
    p = os.path.join(tmp, JETS[k]["asm"] + ".dll")
    compile_dll(k, p, unified=True)
    same = open(p, "rb").read() == open(release_dll, "rb").read()
    print(f"  byte-for-byte vs {release_dll}: {'IDENTICAL' if same else 'DIFFERENT'} (rebuilt sha256 {sha(p, 16)}, release {sha(release_dll, 16)})")
    shutil.rmtree(tmp)
    return same

# ---------------------------------------------------------------------------------------------------------------------------
# the bench

def data_paths(k):
    """(airframe files, damage file) the bench should use, from env, your saved folder, or the tree"""
    j = JETS[k]; t = tree(k); loc = load_local().get("data", {}).get(k)
    files = []
    for rel in j["data"]:
        name = os.path.basename(rel)
        p = (os.path.join(loc, name) if loc else None)
        if not (p and os.path.isfile(p)):
            p = os.path.join(t, rel)
        files.append(p)
    if os.environ.get("BENCH_DATA"):
        files[0] = os.environ["BENCH_DATA"]
    dmg = None
    if j["dmg"]:
        name = os.path.basename(j["dmg"])
        dmg = os.environ.get("BENCH_DMG") or ((os.path.join(loc, name) if loc and os.path.isfile(os.path.join(loc, name)) else None)) or os.path.join(t, j["dmg"])
    return files, dmg


def data_status(k):
    files, _ = data_paths(k)
    miss = [os.path.basename(f) for f in files if not os.path.isfile(f)]
    if miss:
        return False, "missing " + ", ".join(miss)
    return True, os.path.dirname(files[0])


def bench_env(k):
    files, dmg = data_paths(k)
    e = dict(os.environ)
    e["BENCH_DATA"] = files[0]
    if dmg:
        e["BENCH_DMG"] = dmg
    e["BENCH_DIR"] = os.path.join(tree(k), "bench")
    return e


def need_bench(k):
    t = tree(k)
    if not os.path.isfile(os.path.join(t, "bench", "bin", "bench.dll")):
        die("the bench is not built yet.", f"python fcs.py bench {k} build")
    st, where = data_status(k)
    if not st:
        die(f"the {k} bench has no airframe data ({where}).", f"python fcs.py data {k}   (explains how to make it from your game copy)")


def cmd_bench(a):
    k = jet_key(a.jet); t = tree(k)
    bash = find_bash()
    if a.action == "build":
        if not bash:
            die("building the bench runs the tree's bash scripts, and no bash was found.",
                "Windows: install Git for Windows (gives Git Bash) or WSL, then run this again.")
        if not find_dotnet():
            die("no dotnet found.", "install the .NET 8 SDK from dot.net")
        print(f"\nBuilding the {k} bench (syncs the FCS sources into bench/fcs, compiles bench/bin/bench.dll, and the native core if Rust is installed)...")
        e = dict(os.environ)
        if shutil.which("rustup") and not e.get("RUSTUP_TOOLCHAIN"):
            e["RUSTUP_TOOLCHAIN"] = "stable"     # the Vortex pins 1.97.0; offline rustup cannot fetch it
        r = subprocess.run([bash, "bench.sh", "build"], cwd=t, env=e, capture_output=True, text=True)
        log = r.stdout + r.stderr
        okb = os.path.isfile(os.path.join(t, "bench", "bin", "bench.dll")) and " error " not in log
        so = glob.glob(os.path.join(t, "bench", "bin", "*FcsCore*"))
        if okb:
            ok("bench built: " + os.path.relpath(os.path.join(t, "bench", "bin", "bench.dll"), KIT))
            ok("native core for the bench: " + (", ".join(os.path.basename(s) for s in so) if so else "none (no Rust): the bench runs the C# twin, same results"))
            st, where = data_status(k)
            ok("airframe data: " + (where if st else f"MISSING ({where}) -> python fcs.py data {k}"))
        else:
            print(log[-3000:])
            die("the bench did not build (messages above).", "run `python fcs.py doctor`; if you edited the FCS, fix the first compile error.")
    elif a.action == "info":
        need_bench(k)
        subprocess.run([find_dotnet(), "bin/bench.dll", "info"], cwd=os.path.join(t, "bench"), env=bench_env(k))


def stick_args(k, p, kv):
    """turn a preset into the jet's bench command line"""
    ias = kv.pop("ias", "400"); alt = kv.pop("alt", "1500"); thr = kv.pop("thr", "1")
    T = float(kv.pop("T", p["T"]))
    if JETS[k]["bench"] == "one":     # F-22E bench: profiles counted from the pull start
        a = ["one", f"iasKmh={ias}", f"alt={alt}", "trimT=1", "throttle=-1", f"thr2={thr}", "thr2T=0", f"prof={p['pitch']}", f"rprof={p['roll']}",
             f"hold={T}", "post=0"]
        if p.get("yaw"):
            a.append(f"yprof={p['yaw']}")
        return a
    a = ["ifly", f"ias={ias}", f"alt={alt}", f"thr={thr}", f"T={T}", f"pitch={p['pitch']}", f"roll={p['roll']}"]
    if p.get("yaw"):
        a.append(f"yaw={p['yaw']}")
    return a


def cmd_presets(a):
    print("\nManoeuvres for `python fcs.py fly <jet> <preset>` (default 400 km/h, 1500 m, full throttle):\n")
    for n, p in PRESETS.items():
        print(f"  {n:10s} {p['what']}")
    print("\n  Change them on the command line: ias=550 alt=3000 thr=0.5 T=10, and any FCS setting, e.g. MaxPitchRate=48 DisableTVC=true.")
    print("  Your own stick shape: pitch=0:0,0.5:1,4:1 roll=0:0,1:1,1.1:0 (time:stick points, straight lines between).\n")


def run_dotnet(k, args, env):
    t = tree(k)
    stats = tempfile.NamedTemporaryFile(delete=False, suffix=".txt"); stats.close(); os.remove(stats.name)
    env = dict(env); env["FCS_NATIVE_STATS"] = stats.name
    t0 = time.time()
    r = subprocess.run([find_dotnet(), "bin/bench.dll"] + args, cwd=os.path.join(t, "bench"), env=env, capture_output=True, text=True)
    dt = time.time() - t0
    nat = open(stats.name).read().strip() if os.path.exists(stats.name) else ""
    if os.path.exists(stats.name):
        os.remove(stats.name)
    return r, dt, nat


def native_line(nat):
    if not nat:
        return None
    d = dict(x.split("=", 1) for x in nat.split() if "=" in x)
    g = lambda k: int(d.get(k, 0) or 0)
    s, fb, chk = g("solves"), g("fallbacks"), g("checked")
    extra = []
    if g("grid"):
        extra.append(f"{g('grid'):,} table kernels")
    if g("cpl"):
        extra.append(f"{g('cpl'):,} coupled-check LPs")
    tail = (" (" + ", ".join(extra) + ")") if extra else ""
    if chk:
        return (f"native core checked against its C# twin: {chk:,} solves, {g('identical'):,} identical, {g('mismatches')} differences, "
                f"{fb} fallbacks{tail}")
    if s == 0:
        return "native core: not used on this run (the C# solver ran; same laws)"
    return f"native core: in use, {s:,} solves, {fb} fallbacks{tail}"


def cmd_fly(a):
    k = jet_key(a.jet)
    need_bench(k)
    kv = dict(x.split("=", 1) for x in a.settings if "=" in x)
    p = PRESETS.get(a.preset)
    if not p:
        die(f"no preset '{a.preset}'.", "python fcs.py presets")
    runs = os.path.join(KIT, "runs", k); os.makedirs(runs, exist_ok=True)
    stamp = time.strftime("%Y%m%d_%H%M%S")
    out = os.path.abspath(kv.pop("csv", os.path.join(runs, f"{a.preset}_{stamp}.csv")))
    if p.get("stovl"):
        if not JETS[k].get("stovl"):
            die("the hover preset is for the STOVL jet (vortex) only.")
        args = ["stovl", "ias=250", "alt=300", "T=60", "tog=1", "thr=0:0.15,7:0,31:0.25", f"csv={out}"]
    else:
        p = dict(p)
        for key in ("pitch", "roll", "yaw"):
            if key in kv:
                p[key] = kv.pop(key)
        args = stick_args(k, p, kv) + [f"csv={out}"]
    args += [f"{n}={v}" for n, v in kv.items()]
    print(f"\nFlying '{a.preset}' on the {k} bench ({JETS[k]['name']}, FCS {version(k)})...")
    r, dt, nat = run_dotnet(k, args, bench_env(k))
    if r.returncode:
        print((r.stdout + r.stderr)[-2500:])
        if "unknown key" in r.stderr:
            die("the bench does not know one of your Name=value settings (see 'unknown key' above).",
                "check the spelling against docs/CONFIG_REFERENCE.md (settings are case sensitive).")
        die("the bench run failed (messages above).")
    if not os.path.isfile(out):
        print(r.stdout[-1500:]); die("the bench finished but wrote no CSV.")
    ok(f"done in {dt:.1f} s -> {os.path.relpath(out, KIT)}")
    nl = native_line(nat)
    summarize(out, native=nl, plot=a.plot)


def cmd_replay(a):
    k = jet_key(a.jet)
    need_bench(k)
    log = os.path.abspath(a.log)
    if not os.path.isfile(log):
        die(f"no file {a.log}.", "give the path of an FCS telemetry CSV from the game's BepInEx folder (Telemetry on in the FCS config).")
    runs = os.path.join(KIT, "runs", k); os.makedirs(runs, exist_ok=True)
    out = os.path.join(runs, f"replay_{os.path.splitext(os.path.basename(log))[0]}_{a.t0}-{a.t1}.csv")
    args = ["replay", f"log={log}", f"t0={a.t0}", f"t1={a.t1}", f"out={out}"] + a.settings
    print(f"\nReplaying {os.path.basename(log)} {a.t0}-{a.t1} s on the {k} bench: the bench starts from the logged state and flies your logged stick.")
    r, dt, nat = run_dotnet(k, args, bench_env(k))
    if r.returncode or not os.path.isfile(out):
        print((r.stdout + r.stderr)[-2500:]); die("the replay failed (messages above).", "check t0/t1 are inside the log and the log is from this jet's FCS.")
    ok(f"done in {dt:.1f} s -> {os.path.relpath(out, KIT)}  (columns ending _b = bench, _g = game, where the bench writes both)")
    summarize(out, native=native_line(nat), plot=a.plot)


def cmd_suite(a):
    k = jet_key(a.jet); need_bench(k)
    bash = find_bash() or die("suites run the tree's bash scripts and no bash was found.", "Windows: Git Bash or WSL.")
    print(f"\nRunning the {k} regression suite as '{a.tag}' (several minutes; writes bench/out/out_*_{a.tag}.txt)...")
    r = subprocess.run([bash, "bench.sh", "suite", a.tag] + a.settings, cwd=tree(k), env=bench_env(k), text=True, capture_output=True)
    print(r.stdout[-6000:])
    if r.returncode:
        print(r.stderr[-2000:]); die("the suite stopped with an error (above).", "most suites need pandas + numpy: pip install pandas numpy")
    ok(f"next: run your change as another tag, then `python fcs.py compare {k} {a.tag} <other tag>`")


def cmd_compare(a):
    k = jet_key(a.jet); t = tree(k)
    od = os.path.join(t, "bench", "out")
    kinds = sorted({re.match(r"out_(\w+?)_", os.path.basename(f)).group(1) for f in glob.glob(os.path.join(od, f"out_*_{a.a}.txt"))})
    if not kinds:
        die(f"no suite output for tag '{a.a}' in {os.path.relpath(od, KIT)}.", f"python fcs.py suite {k} {a.a}")
    same = True
    for kd in kinds:
        fa, fb = os.path.join(od, f"out_{kd}_{a.a}.txt"), os.path.join(od, f"out_{kd}_{a.b}.txt")
        if not os.path.isfile(fb):
            print(f"  {kd}: no '{a.b}' output"); same = False; continue
        la, lb = open(fa).read().splitlines(), open(fb).read().splitlines()
        if la == lb:
            print(f"  {kd:6s} identical")
            continue
        same = False
        print(f"  {kd:6s} differs:")
        import difflib
        for line in difflib.unified_diff(la, lb, a.a, a.b, n=0, lineterm=""):
            if line.startswith(("---", "+++", "@@")):
                continue
            print("     " + line[:160])
    print("\n  " + ("Identical on every set." if same else
                    "Differences listed. Roll-coupled cases are chaotic: rerun a flagged case with a small change in entry speed before calling it real (MANUAL §10)."))

# ---------------------------------------------------------------------------------------------------------------------------
# reading a run: the numbers that matter and the curve checks Tom asks for (MANUAL §10.3)

def read_csv(p):
    with open(p, newline="") as f:
        r = csv.reader(f)
        hdr = next(r)
        cols = {h: [] for h in hdr}
        for row in r:
            if len(row) != len(hdr):
                continue
            for h, v in zip(hdr, row):
                try:
                    cols[h].append(float(v))
                except ValueError:
                    cols[h].append(float("nan"))
    return cols


def pick(cols, *names):
    for n in names:
        if n in cols and cols[n]:
            return cols[n]
    return None


def smooth(x, n=3):
    if len(x) < n:
        return x[:]
    h = n // 2
    return [sum(x[max(0, i - h):i + h + 1]) / len(x[max(0, i - h):i + h + 1]) for i in range(len(x))]


def curve_checks(t, x, label, unit):
    """quick screens of one rate curve: onset, dips/hitches, spikes, chatter. Not a substitute for looking at the plot."""
    xs = [v for v in x if not math.isnan(v)]
    if len(xs) < 10:
        return []
    ab = [0.0 if math.isnan(v) else abs(v) for v in x]
    pk = max(ab)
    floor = 1.0 if unit == "°" else 5.0      # below this the curve is noise-level; nothing to judge
    if pk < floor:
        return [f"{label}: stays under {floor:g} {unit}, nothing to judge"]
    dt = (t[-1] - t[0]) / max(1, len(t) - 1)
    notes = []
    # onset: first 10 % of peak -> 90 % of peak
    i10 = next((i for i, v in enumerate(ab) if v >= 0.1 * pk), None)
    i90 = next((i for i, v in enumerate(ab) if v >= 0.9 * pk), None)
    if i10 is not None and i90 is not None and i90 >= i10:
        notes.append(f"onset 10->90 % of peak in {(i90 - i10) * dt:.2f} s")
    s = smooth(x, 5)
    # dips: a fall of >15 % of peak from a running high that then recovers by >10 % of peak, while the rate keeps its sign
    dips = []
    hi_v, hi_i, lo_v, lo_i, state = None, None, None, None, 0
    for i, v in enumerate(s):
        av = abs(v)
        if hi_v is None or av > hi_v:
            if state == 1 and lo_v is not None and av - lo_v > 0.10 * pk and hi_v - lo_v > 0.15 * pk:
                dips.append((t[lo_i], hi_v - lo_v))
            hi_v, hi_i, lo_v, state = av, i, None, 0
        elif hi_v - av > 0.15 * pk:
            state = 1
            if lo_v is None or av < lo_v:
                lo_v, lo_i = av, i
        if i > 0 and (s[i - 1] > 0) != (v > 0) and abs(v) > 0.05 * pk:
            hi_v, lo_v, state = av, None, 0     # a sign change starts a new segment (a reversal is not a dip)
    if dips:
        notes.append("DIPS / hitches at " + ", ".join(f"{tt:.2f} s (-{d:.0f} {unit})" for tt, d in dips[:4]))
    else:
        notes.append("no dips")
    # spikes: one sample far from its 0.1 s neighbourhood median
    w = max(2, int(0.05 / dt)) if dt > 0 else 3
    spikes = []
    for i in range(w, len(x) - w):
        nb = sorted(x[i - w:i] + x[i + 1:i + 1 + w])
        med = nb[len(nb) // 2]
        if abs(x[i] - med) > 0.2 * pk:
            spikes.append(t[i])
    notes.append(("SPIKES at " + ", ".join(f"{s_:.2f} s" for s_ in spikes[:4])) if spikes else "no spikes")
    # chatter: direction changes of the rate's slope bigger than 2 % of peak, per second, worst 1 s window
    d = [s[i + 1] - s[i] for i in range(len(s) - 1)]
    turns = [i for i in range(1, len(d)) if d[i - 1] * d[i] < 0 and abs(d[i]) + abs(d[i - 1]) > 0.02 * pk * dt * 20]
    worst = 0
    if dt > 0:
        win = int(1.0 / dt)
        j0 = 0
        for j in range(len(turns)):
            while turns[j] - turns[j0] > win:
                j0 += 1
            worst = max(worst, j - j0 + 1)
    notes.append(f"CHATTER ({worst} slope reversals within 1 s)" if worst >= 8 else "no chatter")
    return [f"{label}: " + " · ".join(notes)]


def summarize(path, native=None, plot=False):
    c = read_csv(path)
    t = pick(c, "t", "time")
    if not t:
        die(f"{path} has no 't' column; it is not a bench or FCS log CSV.")
    n = len(t)
    dur = t[-1] - t[0]
    hz = (n - 1) / dur if dur > 0 else 0
    print(f"\nReadout of {os.path.relpath(path, KIT) if path.startswith(KIT) else path}: {dur:.1f} s, {n} samples ({hz:.0f} per second)\n")
    def rng(name, *cols, unit="", fmt="{:.1f}", absmax=False, why=""):
        x = pick(c, *cols)
        if not x:
            return
        xs = [v for v in x if not math.isnan(v)]
        if not xs:
            return
        if absmax:
            v = max(xs, key=abs)
            line = f"{'worst' if name == 'Sideslip' else 'peak'} {fmt.format(abs(v))}{unit}"
        else:
            line = f"peak {fmt.format(max(xs))}{unit}   lowest {fmt.format(min(xs))}{unit}"
        print(f"  {name:14s} {line:34s} {why}")
    fcs_log = "mode" in c and "rho" in c       # the FCS's own telemetry format (game logs, bench fcslog / replay): speeds in m/s
    ias = pick(c, "ias", "ias_b")
    if ias:
        k = 3.6 if fcs_log else 1.0
        print(f"  {'Speed':14s} {ias[0]*k:.0f} -> {ias[-1]*k:.0f} km/h")
    # the first 0.1 s of a replay is the bench settling onto the logged state (a one-tick acceleration jump): skip it for g
    t0 = t[0] + 0.1
    for col in ("nz", "nz_b"):
        if col in c:
            c[col] = [v if tt >= t0 else float("nan") for v, tt in zip(c[col], t)]
    alt = pick(c, "alt", "alt_b")
    if alt and max(alt) - min(alt) > 0:
        print(f"  {'Height':14s} {alt[0]:.0f} -> {alt[-1]:.0f} m (range {min(alt):.0f}..{max(alt):.0f})")
    vs = pick(c, "vs")
    if vs:
        print(f"  {'Vertical speed':14s} {min(vs):+.2f} .. {max(vs):+.2f} m/s   (STOVL: the hover holds this near zero)")
    rng("AoA", "alpha", "alpha_b", unit="°", why="angle of attack: nose above the airflow")
    rng("Load", "nz", "nz_b", unit=" g", fmt="{:.2f}")
    rng("Sideslip", "beta", "beta_b", unit="°", absmax=True, why="nose off the airflow sideways; smaller is better")
    rng("Roll rate", "pV", "p", "p_b", unit="°/s", fmt="{:.0f}", absmax=True, why="pV = velocity-vector roll where the bench logs it")
    rng("Pitch rate", "q", "q_b", unit="°/s", absmax=True)
    rng("Yaw rate", "r", "r_b", unit="°/s", absmax=True)
    if native:
        print("\n  " + native)
    print("\nCurve checks (quick screens of the rule: instant response, smooth rate, no spike, hitch or chatter):")
    for lab, cols, unit in (("roll rate", ("pV", "p", "p_b"), "°/s"), ("pitch rate", ("q", "q_b"), "°/s"), ("sideslip", ("beta", "beta_b"), "°")):
        x = pick(c, *cols)
        if x:
            for line in curve_checks(t, x, lab, unit):
                print("  " + line)
    print("  These screens flag what to look at; they do not replace looking at the time history (use --plot).\n")
    if plot:
        make_plot(path, c, t)


def make_plot(path, c, t):
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except Exception:
        print("  (no plot: pip install matplotlib)\n")
        return
    rows = [("AoA / sideslip (°)", [("alpha", "AoA"), ("beta", "sideslip")]), ("rates (°/s)", [("p", "roll p"), ("pV", "roll pV"), ("q", "pitch q"), ("r", "yaw r")]),
            ("load (g)", [("nz", "nz")]), ("stick", [("sp", "pitch"), ("sr", "roll"), ("sy", "yaw")])]
    rows = [(ttl, [(col, lab) for col, lab in s if col in c]) for ttl, s in rows]
    rows = [r for r in rows if r[1]]
    fig, ax = plt.subplots(len(rows), 1, figsize=(10, 2.2 * len(rows)), sharex=True)
    ax = ax if len(rows) > 1 else [ax]
    for a_, (ttl, s) in zip(ax, rows):
        for col, lab in s:
            a_.plot(t, c[col], label=lab, lw=1.1)
        a_.set_ylabel(ttl); a_.grid(alpha=0.3); a_.legend(loc="upper right", fontsize=8)
    ax[-1].set_xlabel("time (s)")
    fig.suptitle(os.path.basename(path)); fig.tight_layout()
    png = os.path.splitext(path)[0] + ".png"
    fig.savefig(png, dpi=110)
    print(f"  plot: {png}\n")


def cmd_summary(a):
    summarize(os.path.abspath(a.csv), plot=a.plot)

# ---------------------------------------------------------------------------------------------------------------------------
# airframe data

def cmd_data(a):
    k = jet_key(a.jet); j = JETS[k]; t = tree(k)
    if a.set:
        d = load_local(); d.setdefault("data", {})[k] = os.path.abspath(a.set); save_local(d)
        ok(f"the {k} bench will read its airframe files from {os.path.abspath(a.set)}")
    st, where = data_status(k)
    print(f"\n{j['name']}: airframe data {'FOUND (' + where + ')' if st else 'MISSING'}\n")
    files, dmg = data_paths(k)
    print("  The bench looks for: " + ", ".join(os.path.basename(f) for f in files) + (f" (+ damage file {os.path.basename(dmg)})" if dmg else ""))
    print("  in this order: BENCH_DATA / BENCH_DMG, the folder you saved with `fcs.py data " + k + " --set <folder>`, then the tree's bench folders.\n")
    if j["owner"] == "game":
        pf = j.get("prefab", "<prefab name>")
        print("  This is a stock jet: you make the file from your own copy of the game (the game's data is not published).")
        print("    1. pip install UnityPy")
        print('    2. set NO_GAME to your Nuclear Option folder (or run `python fcs.py setup --game ...`)')
        print(f"    3. python {os.path.relpath(os.path.join(t, 'bench', 'calib', 'stock_extract.py'), KIT)} {pf} {os.path.basename(files[0])}")
        print(f"       (the damage file: bench/extract/stock_damage.py; MANUAL §9.2)")
        print(f"    4. python fcs.py data {k} --set <the folder with the files>")
    elif k == "j20b":
        print("  The J-20B bench flies the mod's own airframe, extracted from your copy of the J-20B Weilong mod (Reasonix's; not ours to publish):")
        print("    1. pip install UnityPy numpy scipy")
        print(f"    2. python {os.path.relpath(os.path.join(t, 'bench', 'extract', 'j20_extract.py'), KIT)} <the mod's .nobp bundle> <its RuntimeContract.json> j20_airframe.json")
        print("       (a mod shipped as one DLL carries the bundle inside: cut it out from the 'UnityFS' marker, MANUAL §9.2)")
        print(f"    3. python fcs.py data {k} --set <the folder with j20_airframe.json>")
        print("  With any other airframe file the bench stops: the FCS waits for the J-20B mod's surface system, which the bench builds")
        print("  only from a J-20 file (its \"j20\" block).")
    else:
        print(f"  This jet comes from {'a mod' if j['owner'] == 'mod' else 'a Blueprinter design'} that is not ours to publish.")
        print("  Extract it from your copy with the scripts in bench/extract/ (MANUAL §9.2 and §4.3 of AGENTS.md), or ask the FCS author,")
        print(f"  then: python fcs.py data {k} --set <the folder with the files>")
    print()

# ---------------------------------------------------------------------------------------------------------------------------
# porting

def cmd_new(a):
    na = os.path.join(KIT, "tools", "new_airframe.py")
    ask = lambda q, d=None: (input(f"  {q}{' [' + d + ']' if d else ''}: ").strip() or d)
    if not a.args:
        print("\nNew FCS port. A few questions (Enter keeps the suggestion):\n")
        print("  Which reference is closest? " + ", ".join(k for k in JETS if k != "j20b") + "  (python fcs.py list explains them)")
        frm = jet_key(ask("start from", "revoker"))
        title = ask("aircraft title as players see it, e.g. 'MiG-29A Fulcrum'")
        short = ask("short name for logs, e.g. 'MiG29'", re.sub(r"[^A-Za-z0-9]", "", (title or "Jet"))[:8])
        ns = ask("C# namespace, e.g. 'MiG29_A.FCS'", f"{short}_FCS.FCS".replace("_FCS.FCS", ".FCS"))
        match = ask("text in the aircraft's name that identifies it in the game, e.g. 'MiG-29'", title)
        prefix = ask("file prefix for DLL / core names", short)
        out = ask("output folder", os.path.join(KIT, "ports", short))
        argv = ["--from", frm, "--out", out, "--ns", ns, "--title", title, "--short", short, "--file-prefix", prefix, "--match", match]
    else:
        argv = a.args
    print()
    r = subprocess.run([sys.executable, na] + argv)
    if r.returncode == 0:
        print("\n  Next: open PORT_TODO.md in the new folder and work it top to bottom (MANUAL §7).")
        print("  Build it with build.sh there, or copy the folder into reference/ to use `fcs.py build`.\n")

# ---------------------------------------------------------------------------------------------------------------------------

def cmd_check(a):
    """the shared asymmetric-load files: identical on every jet apart from the namespace line? (MANUAL §2.19)"""
    lines = []
    for k, j in JETS.items():
        lines.append(f"{j['folder']} {os.path.join(tree(k), j['plugin'])}")
    tmp = tempfile.NamedTemporaryFile("w", delete=False, suffix=".txt"); tmp.write("\n".join(lines) + "\n"); tmp.close()
    script = os.path.join(tree("compass"), "tools", "asym_shared", "checkshared.py")
    print("\nShared asymmetric-load files (AsymShared.cs, AsymCap.cs, CoupledCap.cs), namespace line ignored:\n")
    r = subprocess.run([sys.executable, script, tmp.name], capture_output=True, text=True)
    os.remove(tmp.name)
    out = r.stdout.splitlines()
    for line in out:
        if line.startswith("per-jet settings"):
            break
        if "DIFFERS" in line:
            name = line.split()[0]
            groups = {}
            for jet, h in re.findall(r"'(\w+)': '([0-9a-f]+)'", line):
                groups.setdefault(h, []).append(jet)
            print(f"  {name:15s} DIFFERS: " + "; ".join(f"{h[:8]} on {', '.join(v)}" for h, v in groups.items()))
        else:
            print("  " + line)
    if any("DIFFERS" in l for l in out):
        print("\n  A shared file that differs means a fix reached some jets and not others. Copy the newest text to every jet")
        print("  (only the namespace line differs) and bench each one, or record why a jet keeps its own (MANUAL §13 F45).\n")
    else:
        print("\n  All shared files identical.\n")


def cmd_help(_a=None):
    print(__doc__)


def main():
    if len(sys.argv) == 1:
        cmd_help(); return
    if sys.argv[1] == "new":       # pass new_airframe.py's own options straight through
        cmd_new(argparse.Namespace(args=sys.argv[2:])); return
    ap = argparse.ArgumentParser(prog="fcs.py", description="One front door to the NO FCS kit. Run without arguments for the overview.")
    sp = ap.add_subparsers(dest="cmd")
    sp.add_parser("help").set_defaults(f=cmd_help)
    sp.add_parser("doctor", help="check your tools").set_defaults(f=cmd_doctor)
    sp.add_parser("list", help="the reference jets").set_defaults(f=cmd_list)
    sp.add_parser("presets", help="the bench manoeuvres").set_defaults(f=cmd_presets)
    p = sp.add_parser("setup", help="save your game folder"); p.add_argument("--game"); p.set_defaults(f=cmd_setup)
    p = sp.add_parser("build", help="build a plugin: main DLL + native core (+ unified with --unified)")
    p.add_argument("jet"); p.add_argument("--unified", action="store_true", help="also build the single-file DLL (core embedded) as an optional zip")
    p.add_argument("--core-in-main", action="store_true", help="put the native core DLL into the main zip too (both files, side by side)")
    p.add_argument("--out", help="output folder (default out/<Jet>-<version>)")
    p.add_argument("--check", metavar="RELEASE_DLL", help="also rebuild the unified DLL and compare it byte for byte with a release DLL")
    p.set_defaults(f=cmd_build)
    p = sp.add_parser("bench", help="build the bench (bench <jet> build) or show its airframe (info)")
    p.add_argument("jet"); p.add_argument("action", choices=["build", "info"]); p.set_defaults(f=cmd_bench)
    p = sp.add_parser("fly", help="fly a preset manoeuvre on the bench")
    p.add_argument("jet"); p.add_argument("preset"); p.add_argument("settings", nargs="*", help="ias=.. alt=.. thr=.. T=.. Name=value")
    p.add_argument("--plot", action="store_true"); p.set_defaults(f=cmd_fly)
    p = sp.add_parser("replay", help="replay a window of your flight log on the bench")
    p.add_argument("jet"); p.add_argument("log"); p.add_argument("t0"); p.add_argument("t1"); p.add_argument("settings", nargs="*")
    p.add_argument("--plot", action="store_true"); p.set_defaults(f=cmd_replay)
    p = sp.add_parser("suite", help="the regression suite under a tag"); p.add_argument("jet"); p.add_argument("tag"); p.add_argument("settings", nargs="*")
    p.set_defaults(f=cmd_suite)
    p = sp.add_parser("compare", help="compare two suite tags"); p.add_argument("jet"); p.add_argument("a"); p.add_argument("b"); p.set_defaults(f=cmd_compare)
    p = sp.add_parser("summary", help="plain-English readout of a CSV"); p.add_argument("csv"); p.add_argument("--plot", action="store_true")
    p.set_defaults(f=cmd_summary)
    p = sp.add_parser("data", help="airframe data for a bench: status and how to make it"); p.add_argument("jet"); p.add_argument("--set", metavar="FOLDER")
    p.set_defaults(f=cmd_data)
    sp.add_parser("check", help="are the shared asymmetric-load files still identical on every jet").set_defaults(f=cmd_check)
    p = sp.add_parser("new", help="start a port (asks questions; or pass new_airframe.py's options)"); p.add_argument("args", nargs=argparse.REMAINDER)
    p.set_defaults(f=cmd_new)
    a = ap.parse_args()
    if not getattr(a, "f", None):
        cmd_help(); return
    a.f(a)


if __name__ == "__main__":
    try:
        main()
    except BrokenPipeError:      # output piped into `head` or similar
        pass
    except KeyboardInterrupt:
        print("\n  stopped.")
