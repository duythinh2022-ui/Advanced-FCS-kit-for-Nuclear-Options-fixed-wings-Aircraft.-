# Quick start

Everything below is one command, `python fcs.py`, run from this folder. You need **Python 3** and the **.NET 8 SDK**
(dot.net). It runs on Windows, Linux and macOS. The full manual is `MANUAL.md` / `NO-FCS-Kit-Manual.pdf`; §4 explains this page.

```text
python fcs.py                         what you can do, in plain words
python fcs.py doctor                  what is installed and what is missing (each missing line says how to fix it)
python fcs.py list                    the eight reference jets, and which one to start a port from
```

## Build a plugin

```text
python fcs.py setup --game "C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option"     once
python fcs.py build revoker           -> out/Revoker-<version>/
```
You get two files, the way NOMM wants it: the **plugin DLL** (C#) and the **native core** `<Lib>.dll` (Rust, faster). Install
both in the same `BepInEx/plugins/<folder>/`. The plugin works without the core, only slower. `--unified` also builds the
single-file DLL with the core inside, as an optional download. The release zips are made for you, named so the player zip
sorts first.

## Try the bench (an offline flight simulator running the real FCS code)

```text
python fcs.py data revoker            how to make the airframe file from your own game copy (it is not in the kit)
python fcs.py bench revoker build     needs bash: on Windows, Git Bash or WSL
python fcs.py presets                 pull, roll, pull-roll, reversal, stop, pushover, corkscrew, hover (Vortex)
python fcs.py fly revoker pull-roll --plot
python fcs.py fly revoker reversal ias=550 MaxPitchRate=48      any entry speed, any FCS setting
python fcs.py replay revoker my_flight.csv 120 135              a window of your own flight log
```
Each run prints a plain-English readout (speed, AoA, g, sideslip, roll / pitch / yaw rates, whether the native core ran) and
curve checks: dips or hitches, spikes, chatter. Results and plots go to `runs/<jet>/`.

## Change something and check you didn't break anything

```text
python fcs.py suite revoker before
  ... edit reference/Revoker-FCS-*/Revoker_FS12.FCS/*.cs ...
python fcs.py bench revoker build
python fcs.py suite revoker after
python fcs.py compare revoker before after
python fcs.py check                   the files all jets share must stay identical
```

## Put it on your own aircraft

```text
python fcs.py new                     asks a few questions, clones the closest jet, writes PORT_TODO.md
```
Then MANUAL §7 (porting), §8 (solved configurations), §24 (airframe aerodynamics; §24.11 for F-22-style coplanar stabs).
