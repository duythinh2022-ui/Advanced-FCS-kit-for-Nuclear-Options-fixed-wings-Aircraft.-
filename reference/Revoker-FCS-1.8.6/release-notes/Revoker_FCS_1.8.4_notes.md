# FS-12 Revoker FCS 1.8.4: Typhoon inner flaperons, third attempt

Base: 1.8.3. Visual only; the flying is identical to 1.8.2 / 1.8.3. **Not seen in the game.**

The 15:31 LogOutput.log (1.8.3) still bound both inner panels to the aileron, with no inner-flap candidate. The FCS creates its inner-flap effectors when it engages on the jet, which can be after the Typhoon mod has built its panels, so at binding time there was no inner-flap visual to offer. 1.8.4 waits (re-binding twice a second, up to 60 s after spawn) while two panels still share one surface, and binds once the inner-flap visuals exist. The log line now also says how many inner-flap visuals were known on the jet and how long after spawn it bound.

Expected in LogOutput.log: `Typhoon_flaperon_in_L ... <- <inner flap visual>`, and "inner-flap visuals known on this jet: 2".
