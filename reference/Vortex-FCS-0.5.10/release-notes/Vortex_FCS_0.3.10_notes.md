# FS-20 Vortex FCS 0.3.10 (17 Nov 2026)

On 0.3.9. One recalibration.

## STOVL pedal yaw: matched to the real F-35B (`StovlYawStopTime` 1.84 → 1.09 s)

**Basis.** Tom (16:12): the real F-35B takes 6–7 s to yaw 90° in the hover at low fuel, consistently in the UK and Italian demos.

**The law is unchanged from 0.3.9.** Full pedal asks for the yaw rate the axis can start and stop within `StovlYawStopTime` at the yaw authority it has now. There is still no fixed limit; only the time constant is recalibrated.

**Calibration.**
- At 20 % fuel (9 640 kg on the bench) the hover yaw authority is 15.3 deg/s².
- A 90° turn in 6.5 s with the start and stop at that authority needs a time constant of 1.09 s, which gives 16.6 deg/s.

**Bench**, pedal held 5.4 s then released, hover:

| Fuel | Mass | Rate limit | Heading change | Stopped (\|r\| < 1 deg/s) |
|---|---|---|---|---|
| 20 % | 9 640 kg | 17.0 deg/s | 94° | 6.55 s after the pedal |
| 100 % | 11 880 kg | 18.1 deg/s | 101° | 6.55 s |

**Migration.** A Dev file that 0.3.9 wrote 1.84 into is moved to 1.09 once (`VortexConfigRevision` 7). A hand-set value is kept.

**Regression.** STOVL set vs 0.3.9: VL, VTO and decel are identical. The hover and transition pedal turns are slower (peak r 31–32 → 18–20 deg/s), and bank differs by up to 4.8° during them. CTOL is unchanged.

Benched, not flown.
