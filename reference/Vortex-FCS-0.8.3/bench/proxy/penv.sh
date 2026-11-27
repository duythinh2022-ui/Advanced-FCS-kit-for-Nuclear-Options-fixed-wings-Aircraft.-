# source: sets the bench env for a variant proxy: penv <B|A|C|NAP>
penv() {
  local B=/home/claude/x/work/bench; export BENCH_DIR=$B BENCH_DMG=$B/data/revoker_damage.json
  case "$1" in
    B) export BENCH_DATA=$B/extract/vortex_airframe.json BENCH_SYSTEMS= BENCH_VARIANT=B BENCH_NONSTOCK= ;;
    A) export BENCH_DATA=/home/claude/x/proxy/vortex_A.json BENCH_SYSTEMS=/nonexistent BENCH_VARIANT=A BENCH_NONSTOCK= ;;
    C) export BENCH_DATA=/home/claude/x/proxy/vortex_C.json BENCH_SYSTEMS=/nonexistent BENCH_VARIANT=C BENCH_NONSTOCK=wing1a_L,wing1a_R,wing1b_L,wing1b_R,wing2a_L,wing2a_R,wing2b_L,wing2b_R,wingtip_L,wingtip_R,elevator_L,elevator_R,aileron_L,aileron_R,vstab_L,vstab_R ;;
    NAP) export BENCH_DATA=/home/claude/x/proxy/vortex_NAP.json BENCH_SYSTEMS=/nonexistent BENCH_VARIANT=NA BENCH_NONSTOCK=wing1a_L,wing1a_R,wing1b_L,wing1b_R,wing2a_L,wing2a_R,wing2b_L,wing2b_R,wingtip_L,wingtip_R,elevator_L,elevator_R,aileron_L,aileron_R,wingRoot_L,wingRoot_R ;;
    T) export BENCH_DATA=/home/claude/x/proxy/vortex_T.json BENCH_SYSTEMS= BENCH_VARIANT=T BENCH_NONSTOCK=wing1b_L,wing2b_L,elevator_L,aileron_L,wing1b_R,wing2b_R,aileron_R,elevator_R,wing2a_L,wingtip_L,wing2a_R,wingtip_R ;;
    NA) export BENCH_DATA=/home/claude/x/proxy/vortex_NA.json BENCH_SYSTEMS=/nonexistent BENCH_VARIANT=NA BENCH_NONSTOCK=liftFan,cockpit,wingRoot_L,wingRoot_R,wing1b_L,wing2b_L,elevator_L,aileron_L,wing1b_R,wing2b_R,aileron_R,elevator_R,weaponBay_L,weaponBay_R,wing2a_L,wingtip_L,wing2a_R,wingtip_R ;;
  esac
}
