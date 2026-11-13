# Design history

Hand-off documents written between work sessions on the FCS, kept for their reasoning. They are snapshots: file paths
(`C:\Games\NO mods\...`, `/home/claude/...`, project-doc names like `claude/...md`) refer to the author's machine and project
at the time, versions and line numbers have moved on, and "installed / not flown" statements were true on their date only.

- `RefGov_port_Ifrit_Revoker_handoff.md` (4 Nov 2026): the reference-capacity governor of F-22E 3.0.0, piece by piece, and the
  rules for porting it to airframes with different effectors. The best single document on porting.
- `FCS_performance_handoff.md` (5 Nov 2026): measured CPU cost per tick, determinism, and the plan to thread the estimator.
- `F22E_FCS_handoff_2.31.0.md` (3 Nov 2026): the F-22E law at 2.31.0 and the push-over work in progress.
- `F22E_airframe_X2_handoff_2026-11-09.md` (9 Nov 2026): the build hand-off for the F-22E's relaxed-to-neutral airframe X2
  (shipped as 3.2.0, refined as X2e in 3.2.1): Tom's goals, why the package as asked failed on the bench, the acceptance
  targets, the open items and the phase-2 roll work. Its study kit (bench patch, fitting scripts, configs, results) is in
  `reference/F22E-FCS-4.3.0/studies/x2_study_kit/`, and the 3.2.0 acceptance scripts in `studies/x2_acceptance/`.

- `reference/F22E-FCS-4.3.0/studies/Loaded_reversal_port_handoff_2026-11-10.md` and `F22E_loaded_reversal_4.1.0_to_4.1.2.diff` (10 Nov 2026): the F-22E 4.1.1 + 4.1.2 loaded roll-reversal fix,
  how it was ported to the other five jets, with the diff (README §8.25).

The 10–12 Nov hand-offs for the performance port, the coupled asymmetric-load check and the ground brakes are not copied;
their content is in README §2.18, §8.23, §8.26, §10.8 and §19.8–19.14.

The 6–9 Nov findings notes the manual names as `claude/...md` (push-over holds and sweep caps, low-speed surface lag, fuel
over-burn, game crashes, the F-22E airframe studies, the Su-30 extraction, pitch wall and bench calibration, the BFM shootout)
are the author's project documents and are not copied here; their content is summarised in README §8, §10.7 and §19.
