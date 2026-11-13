# F-22E FCS 2.3.2: released-stick roll-fight cost off

Base: 2.3.1. One default: `RollFightReleased` false (a file holding true is set to false once, revision 12). Nothing else.

## Why
- 2.0.9 flown on 21 Oct 23:29 (same Dev cfg): zero stuck sets in 34 s of calm released flight (nozzles past 8 deg: 0.0 s; rudders past 20: 0.1 s), against 7.3 / 2.8 s on 2.2.29 and 1.3-1.8 / 0.4-2.0 s on 2.3.0. 2.0.9 has the stab downwash (2.0.8), so that theory was wrong: the stabs sit past their lift peak in 2.0.9's calm flight as much as in 2.2.29's (6.0 s against 10.3) without anything getting stuck.
- What 2.0.9 does not have is 2.2.16's roll-fight cost with the stick centred. Bench, replays of five releases from the 21 Oct logs with the 2.3.1 window off: nozzle peak in the calm phase 20 / 5.3 / 1.9 / 17.4 / 8.4 deg -> 4.5 / 4.1 / 1.9 / 3.6 / 3.4 with `RollFightReleased` false alone; `TvcNeutralHold` 0 alone changes nothing. With the window on as well: the last stuck case (22:51 log, 110.7 s) ends with nozzles 2.4 deg and stabs 0.7 / 1.1 instead of 9.5 and -5.3 / -4.0.
- Mechanism: with the wing forbidden to cancel a parked stab's roll, the other stab had to match the parked one's lift (both nose-down), and the nozzles cancelled the pitch to their stop. The cost was added for a TVC-off crossed set (19 Oct) that the settled window should now cover; if that case comes back with TVC off, set it true for that test.

## Not changed
- The yaw brake's roll at low IAS (the roll run-on at the stop) and the 2-4 deg/s^2 pitch residual the allocator leaves with the nozzles unused (the remaining dip, hold accuracy): both still open.

## Not verified
- Not flown. Bench only (replays above; suite not re-run, the change is one gate).
