# J-20B Weilong FCS 0.2.8: works with the J-20B mod 0.0.5

Base: 0.2.7. **Status:** compiled against the game's assemblies. Not flown. No flight-law or setting changes.

Tom (25 Nov 18:12): "make j20 fcs works with the 0.0.5".

## Why 0.2.7 did nothing with Eroge_J20B_Weilong_0.0.5.dll
- 0.0.5 is one BepInEx plugin, `reasonix.j20.single-dll` (Reasonix.J20 0.0.16.37). Its Awake loads ten embedded assemblies (AeroControl, Mechanisms, NozzleThrust, Mfd, WingVapor, BayLoadouts, Carrier, OptionalLoadouts, Sensors, ModernizedMFD) plus the blueprint bundle. Only after that does it register their old plugin identities, `reasonix.j20.aerocontrol` among them.
- The FCS soft-depended only on `reasonix.j20.aerocontrol`. BepInEx can't see that GUID when it orders the plugins, so the FCS could load first. Its bridge then found no surface system, logged "not loaded - nothing to fly", and never tried again.
- The surface API the FCS uses did not change. AeroControl 0.66 -> 0.68: `NativeAeroSystem` only gained `brakeResidual068`, the mod's own canard trim of the air brake, which the FCS overrides while it owns the canards. `Surface`, `SurfaceSpec`, `Camber`, `HighLift` and `StepFromNative` are identical.
- The 0.0.5 blueprint still has every part the FCS looks up (wingRoot / wing1 / wing2 / flap / aileron / canard / tail, L and R, plus forebody and cockpit). The aircraft name is still "J-20B Weilong".

## Change
1. `FcsStandalonePlugin`: added a soft dependency on `reasonix.j20.single-dll`; the old one is kept for 0.0.4.
2. `J20Bridge.Init`: if AeroControl isn't loaded yet, it waits for it once on `AppDomain.AssemblyLoad` and hooks it then. This is event-driven: nothing in a frame path, no retry loop. If the J-20B mod is absent, the FCS stays idle as before.
3. `J20Bridge`: the `PluginInfos["reasonix.j20.aerocontrol"]` instance is used only if its assembly really is `Reasonix.J20.AeroControl`. This guards against the single DLL registering that identity with its own wrapper instance, which would make every type lookup miss.

## Not checked
- Whether 0.0.5 changed the airframe's masses, areas or lift charts. The blueprint bundle is 133 MB against 0.0.4's 120 MB. The FCS's tuning (CG target, lift scales) assumes 0.0.4's numbers.
- Flight. Check the log for `J-20B FCS: hooked Reasonix.J20.AeroControl 0.68.0.0`.

## Build check
The unchanged 0.2.7 source rebuilt with this toolchain gives the shipped 0.2.7's size (1,445,376 bytes) and identical type / method / field tables. 0.2.8's metadata differs from it only by the three new bridge methods, their three fields and the extra dependency attribute.
