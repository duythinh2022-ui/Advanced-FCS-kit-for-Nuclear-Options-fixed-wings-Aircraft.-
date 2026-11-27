# J-20B Weilong FCS 0.2.5: no nose dip when the air brake opens in flight

Base: 0.2.4. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 16:14): "check latest j20 log, fix the pitch down issues when deploy airbrakes on air, the jet pitches down a bit for a moment then pitch rate settles at 0".

## What was wrong
In the 16:11 log every in-air deploy dips the nose by 2.4 to 3.2 deg/s over about 0.4 s, then the pitch rate comes back to zero with the nose lower. The bench shows the same thing (580 km/h: -5.7 deg/s, nose 4 deg lower after 3 s).

The in-air brake set has three parts: outer elevons trailing edge (TE) up, inner elevons TE down, fins toed. The elevon parts go through the allocator (the solver that splits the pitch/roll/yaw demand across the surfaces), so it plans the canards against them. The fin toe did not. It was added at the hinge drive, after the allocator had already finished. So the allocator balanced the elevon set it was commanding, which on its own is net nose-up (outer +66, inner -56 deg/s^2), and eased the canards off. Meanwhile the fins' toe-out added -15 deg/s^2 each that the allocator never saw. Its model (dHat, the measured pitch acceleration minus the modelled one) was continuous. The canards only answered once the INDI loop (the incremental controller that corrects from the measured pitch acceleration) saw the dip, which is why the rate settles at zero but the attitude lost stays lost.

## Changes
1. **The fin toe is planned by the allocator** (shared `TeBrakeFinToeAlloc`, default off; on for the J-20B from Plugin). Each fin's toe position becomes its allocator anchor (a soft target with a cost per degree away from it), and its window is opened to reach it. The toe and the canards' answer to it now come out of the same solve. The fins' toe force and moment also go into the set's lift balance (`TeBrakeLiftBalance`).
2. **The fins toe in** (new setting `AirBrakeFinToeIn`, 7. Airbrake, on). With change 1 alone (toe-out kept), the dip went away but the jet ballooned: 2.0 g at 580 km/h, 2.6 g at 800, attitude held. The set was never lift-neutral. The inner TE-down lift, plus the canard lift needed to trim the fins' nose-down moment, is more than the outer pair can cancel within its 30 deg TE-up cap. Full open at 800 km/h, the uncancelled lift was 84 % of the inner elevons' and fins' own lift. In 0.2.4 the nose drop hid this. On the J-20B's out-canted fins, toe-in pushes down and nose-up, which works against the inner pair's lift and nose-down moment instead of adding to them. The outer pair then balances the set at 26-29 deg TE up with room to spare. Off = the old toe-out (F-22E style).

No changed defaults, so no config migration. Port `TeBrakeFinToeAlloc` to the other jets on the next shared sync (default off; it only matters where the fins toe at the drive).

## Bench: brake opened in level flight, 3000 m, stick free, AoA/g mode
| IAS (km/h) | min pitch rate (deg/s) | g range | nose change after 3 s (deg) |
|---|---|---|---|
| 300 | -2.10 -> **-0.39** | 1.05-1.34 -> 1.05-1.13 | -2.11 -> -0.44 |
| 400 | -3.17 -> **-0.37** | 0.97-1.42 -> 0.97-1.07 | -2.71 -> -0.26 |
| 580 | -5.73 -> **-0.12** | 0.83-1.68 -> 1.02-1.18 | -4.12 -> -0.02 |
| 800 | -9.16 -> **-0.27** | 0.30-1.19 -> 0.98-1.13 | -6.50 -> -0.08 |

**Retract** (closed after 3 s open): 0.2.4 dipped too, down to 0.46 / -0.02 / -0.79 g at 400 / 580 / 800 km/h. Now it stays at 0.98-1.19 g, pitch rate within 0.3 deg/s.

**Brake out, then full roll** (550 km/h): peak roll 230 -> 237 deg/s, peak sideslip 2.29 -> 2.07 deg.

**Brake out, then half-aft pull** (550 km/h): peak AoA 18.7 in both; the brake's AoA fade is unchanged.

**Runs without the brake:** byte-identical CSV (pull + roll set).

## Trade-off: the brake slows the jet less
Speed change over 3 s against the same run without the brake:

| IAS (km/h) | 0.2.4 | 0.2.5 | 0.2.5 with AirBrakeFinToe 25 |
|---|---|---|---|
| 400 | -19.1 | -10.1 | -12.6 |
| 580 | -31.4 | -21.6 | -26.5 |
| 800 | -49.1 | -39.4 | -47.9 |

Part of 0.2.4's drag was a fight. The allocator, not knowing about the drive toe, pushed the fins another ~15 deg further out (to about 26 deg) as a nose-down effector against nose-up canards. If you want 0.2.4's deceleration back, set AirBrakeFinToe to 25: it stays lift-neutral (580 km/h: pitch rate within 0.16 deg/s, peak 1.17 g).

Whether the real J-20 toes its fins in or out for its air brake is not established here; the toe direction was picked for what the model's fin geometry does.
