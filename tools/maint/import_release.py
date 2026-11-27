#!/usr/bin/env python3
"""Import one FCS release's Source zip into the kit as reference/<Jet>-FCS-<version>/ (kit maintainers only).

    python3 tools/maint/import_release.py <Source zip or unpacked folder> <jet> <version> [--extra-tools DIR]

    <jet>  one of: F22E Ifrit Revoker Compass Ternion Su30 Vortex J20B (the kit's folder prefix)

What it does, in the same way every kit edition since 3.1 was normalised:
  1. finds the tree root inside the zip (the folder holding build.sh / bench.sh);
  2. moves release notes and hand-off docs from the root into release-notes/;
  3. removes what the kit never publishes:
       - airframe / damage / systems JSONs (the game's own data or other people's mods),
         F-22E bench/data/airframe_cal.json and f22e_damage.json (Aryx's mod);
       - the bench's synced FCS copy (bench/fcs, rebuilt by `bench build`);
       - build output (bin/ obj/ out/ target/ __pycache__) and *.orig *.old *.bak files;
       - F-22E analysis/suite_outputs (bench output);
  4. renames the F-22E analysis/ folder to studies/ (excluded from the build, as before);
  5. writes bench/extract/AIRFRAME_DATA_NOT_INCLUDED.md;
  6. makes the tree's .gitignore keep native/prebuilt/*.dll.
The previous reference/<Jet>-FCS-* folder is deleted (git keeps the history).
"""
import argparse, os, re, shutil, sys, tempfile, zipfile

KIT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# what the AIRFRAME_DATA_NOT_INCLUDED note says per jet: (aircraft, owner sentence, files)
OWNERS = {
    "F22E":    ("Aryx's F-22E Strike Raptor mod", "a third-party mod (Aryx)", "`airframe_cal.json`, `f22e_damage.json` in `../data/`"),
    "Ifrit":   ("KR-67 Ifrit", "the game", "`ifrit_airframe.json`, `../data/ifrit_damage.json`"),
    "Revoker": ("FS-12 Revoker", "the game", "`rev_airframe.json`, `../data/revoker_damage.json`"),
    "Compass": ("T/A-30 Compass", "the game", "`compass_airframe.json`, `../data/compass_damage.json` (the bench also borrows the Revoker's)"),
    "Ternion": ("FS-3 Ternion", "a Blueprinter design by another author", "`ternion_airframe.json`, `../data/ternion_damage.json`"),
    "Su30":    ("Su-30VXN Gandiva", "a third-party mod (kyleisred)", "`su30_airframe.json` and damage files"),
    "Vortex":  ("FS-20 Vortex", "the game", "`vortex_airframe.json`, `vortex_systems.json`, the borrowed Revoker files"),
    "J20B":    ("J-20B Weilong", "a third-party mod (Reasonix)", "`j20_airframe.json` here, made by `j20_extract.py` from the mod's bundle (any other airframe file stops the run: the FCS waits for the mod's surface system)"),
}

KEEP_ROOT_MD = {"README.md", "CHANGELOG.md", "PORTABLE_REPLAY.md", "PORT_TODO.md", "QUICKSTART.md"}
DATA_RX = re.compile(r".*(_airframe|_damage|_systems)\.json$|^airframe_cal\.json$")
JUNK_DIRS = {"bin", "obj", "out", "target", "__pycache__", ".vs"}
JUNK_EXT = (".orig", ".old", ".bak", ".pyc")


def find_root(top):
    for dp, dn, fn in os.walk(top):
        dn[:] = [d for d in dn if d not in JUNK_DIRS]
        if "build.sh" in fn and ("bench.sh" in fn or "bench" in dn):
            return dp
    sys.exit(f"no tree with build.sh + bench under {top}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("source"); ap.add_argument("jet", choices=sorted(OWNERS)); ap.add_argument("version")
    ap.add_argument("--extra-tools", help="F-22E: the release's outer tools/ folder, merged into tools/battery/")
    a = ap.parse_args()

    tmp = tempfile.mkdtemp()
    if os.path.isdir(a.source):
        shutil.copytree(a.source, os.path.join(tmp, "x"))
    else:
        zipfile.ZipFile(a.source).extractall(os.path.join(tmp, "x"))
    root = find_root(os.path.join(tmp, "x"))
    dest = os.path.join(KIT, "reference", f"{a.jet}-FCS-{a.version}")
    for old in os.listdir(os.path.join(KIT, "reference")):
        if old.startswith(a.jet + "-FCS-"):
            shutil.rmtree(os.path.join(KIT, "reference", old))
    shutil.copytree(root, dest)
    removed, moved = [], 0

    # 2. notes into release-notes/
    rn = os.path.join(dest, "release-notes"); os.makedirs(rn, exist_ok=True)
    for f in sorted(os.listdir(dest)):
        p = os.path.join(dest, f)
        if os.path.isfile(p) and (f.endswith(".md") or f.endswith(".diff")) and f not in KEEP_ROOT_MD:
            shutil.move(p, os.path.join(rn, f)); moved += 1

    # 4. F-22E analysis -> studies (suite outputs dropped); extra tools -> tools/battery
    an = os.path.join(dest, "analysis")
    if os.path.isdir(an):
        so = os.path.join(an, "suite_outputs")
        if os.path.isdir(so):
            shutil.rmtree(so); removed.append("analysis/suite_outputs/")
        st = os.path.join(dest, "studies")
        if os.path.isdir(st):
            for f in os.listdir(an):
                shutil.move(os.path.join(an, f), os.path.join(st, f))
            shutil.rmtree(an)
        else:
            os.rename(an, st)
        for f in ("F22E_loaded_reversal_4.1.0_to_4.1.2.diff", "Loaded_reversal_port_handoff_2026-11-10.md"):
            if os.path.exists(os.path.join(rn, f)):
                shutil.move(os.path.join(rn, f), os.path.join(st, f))
    if a.extra_tools:
        bt = os.path.join(dest, "tools", "battery"); os.makedirs(bt, exist_ok=True)
        for f in os.listdir(a.extra_tools):
            s = os.path.join(a.extra_tools, f)
            if os.path.isfile(s):
                shutil.copy2(s, os.path.join(bt, f))

    # 3. data and junk
    for dp, dn, fn in os.walk(dest, topdown=True):
        rel = os.path.relpath(dp, dest)
        for d in list(dn):
            if d in JUNK_DIRS or (d == "fcs" and os.path.basename(dp) == "bench"):
                shutil.rmtree(os.path.join(dp, d)); removed.append(os.path.join(rel, d) + "/"); dn.remove(d)
        for f in fn:
            if DATA_RX.match(f) or f.endswith(JUNK_EXT) or f == "f22e_damage.json":
                os.remove(os.path.join(dp, f)); removed.append(os.path.join(rel, f))

    # 5. the data note
    ex = os.path.join(dest, "bench", "extract"); os.makedirs(ex, exist_ok=True)
    name, owner, files = OWNERS[a.jet]
    stock = owner == "the game"
    with open(os.path.join(ex, "AIRFRAME_DATA_NOT_INCLUDED.md"), "w", newline="\n") as f:
        f.write("# Airframe data not included\n\n")
        if stock:
            f.write(f"This jet is a stock Nuclear Option aircraft ({name}). Its airframe and damage files are extracted from the game's own\n"
                    "assets, which belong to the game's developer, so they are not published with the kit.\n\n")
        else:
            f.write(f"This FCS flies {name}, which belongs to {owner}. Its airframe files come out of that work, so they are not\n"
                    "published with the kit.\n\n")
        f.write(f"Files the bench expects: {files}.\n\n"
                "Make them from your own copy of the game / mod with the extraction scripts in this folder (MANUAL §9.2), or run\n"
                "`python fcs.py data <jet>` from the kit's root for the step-by-step. Without them the bench still **builds**;\n"
                "`fly`, `suite` and `replay` need the files (or `BENCH_DATA` / `BENCH_DMG` pointing at them).\n")

    # 6. .gitignore
    gi = os.path.join(dest, ".gitignore")
    txt = open(gi).read() if os.path.exists(gi) else "bin/\nobj/\n*.pdb\n*.bak\n*.zip\n"
    if "!native/prebuilt/*.dll" not in txt:
        txt = txt.rstrip("\n") + "\n\n# keep the prebuilt native core (shipped beside the plugin, or embedded with --unified)\n!native/prebuilt/*.dll\n"
    open(gi, "w", newline="\n").write(txt)

    shutil.rmtree(tmp)
    print(f"{a.jet} {a.version} -> {os.path.relpath(dest, KIT)}: {moved} notes moved, {len(removed)} items removed")
    for r in removed:
        print("  -", r)


if __name__ == "__main__":
    main()
