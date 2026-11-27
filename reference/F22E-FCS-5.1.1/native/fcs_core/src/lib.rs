//! F-22E Strike Raptor FCS: the control allocator's solve, native.
//!
//! A line-for-line port of `Allocator.cs` (Solve / SolveInner / ReachFrom / SolveCore / PairedSearch / SolveLinear and
//! `Effector.MAt`). The C# allocator stays in the plugin as the reference and the fallback; this library must return the
//! same bits for the same inputs, so:
//!
//! * every expression keeps the C# source's operand types, operation order and float <-> double conversions
//!   (C# `float` = `f32`, `double` = `f64`; a `float` operand next to a `double` one is widened first, as in C#);
//! * nothing here calls a transcendental function. The solve needs only + - * /, comparisons, conversions and square
//!   roots, all of which IEEE 754 defines exactly, so the result does not depend on whose maths library is linked.
//!   (That is also why only this part of the FCS is native: the table rebuild, the asymmetric-load sweep and the laws
//!   call sin / cos / atan2 inside the game's own runtime, and no outside library can reproduce those bit for bit.)
//! * Rust never contracts a*b+c into a fused multiply-add and never reorders floating-point operations.
//!
//! The library keeps no state between calls and allocates nothing: any number of threads may solve at once, each on its
//! own buffers.
//!
//! 4.1.0 adds the grid kernels (`f22e_fcs_grid`): a control surface's moment at every deflection of its grid in one
//! airflow, for the table rebuild (`SurfaceEffector.Eval` over the grid) and for the asymmetric-load sweep's capacity loop
//! (`SurfaceEffector.EvalFlowGrid` + the min / max the sweep keeps). The plugin works out everything that needs sin / cos
//! (the rotations, `SurfaceEffector.GridGeom`); what is left here is + - * /, square roots, the airfoil table's linear
//! interpolation and one `atan2` per evaluation, written as C# writes it: `(float)Math.Atan2((double)y, (double)x)`.
//! On the bench both sides call the same C library's `atan2`, so the kernels are bit-identical there too (the suite
//! checks it); in the game they are, like the solve, the bench's arithmetic and not Mono's.

#![allow(non_snake_case)]
#![allow(clippy::needless_range_loop, clippy::too_many_arguments, clippy::excessive_precision)]

use core::ops::{Add, Sub};

/// Bumped whenever the layout or the meaning of the structures below changes. The plugin refuses any other value.
// F-22E 4.2.0: the coupled asymmetric-load check's LP (CoupledCap.cs), from the Ternion 0.3.0 core
pub mod cpl;
pub use cpl::*;

pub const ABI: i32 = 5101000;

pub const MAX_EFF: usize = 16;
pub const MAX_N: usize = 32;

// ---- F-22E 5.0.0: the pair search (Allocator.PairBlockSearch)
pub const PAIR_KINDS: usize = 5;
const MAX_BLOCKS: usize = 16;
const MAX_CAND: usize = 34;
const TRIAL: usize = MAX_CAND - 1;

/// The pair search's caches (Allocator: ecN.. / mtOk.. / mvAll.. and the block state). One per thread, made on its first use and
/// reused, so a solve neither allocates nor clears them; nothing in them is read before this solve writes it (the C# allocator
/// keeps the same arrays between solves).
struct PairScratch {
    ec_n: [i32; MAX_EFF],
    ec_c: [f32; MAX_EFF * MAX_CAND],
    ec_mx: [f32; MAX_EFF * MAX_CAND],
    ec_my: [f32; MAX_EFF * MAX_CAND],
    ec_ex: [f32; MAX_EFF * MAX_CAND],
    ec_ey: [f32; MAX_EFF * MAX_CAND],
    ec_ez: [f32; MAX_EFF * MAX_CAND],
    ec_reg: [f32; MAX_EFF * MAX_CAND],
    ec_gi: [i32; MAX_EFF * MAX_CAND],
    mt_ok: [bool; 2 * MAX_EFF],
    mt_s: [f32; 2 * MAX_EFF * MAX_CAND],
    mt_t1: [f64; 2 * MAX_EFF * MAX_CAND],
    mt_t2: [f64; 2 * MAX_EFF * MAX_CAND],
    mt_t3: [f64; 2 * MAX_EFF * MAX_CAND],
    mt_d: [f64; 2 * MAX_EFF * MAX_CAND],
    mv_all: [V3; MAX_EFF],
    rt_all: [f64; MAX_EFF],
    py_all: [f64; MAX_EFF],
    y0a: [f64; MAX_EFF],
    base_g: [f64; FIGHT_GROUPS + 1],
    base_p1: f64,
    base_p2: f64,
    e0x: f32,
    e0y: f32,
    e0z: f32,
    jp: i32,
    jp2: i32,
    js: i32,
    b_a: usize,
    b_b: usize,
    o_a: usize,
    o_b: usize,
    t_a: usize,
    t_b: usize,
    mt_pk: i32,
    mt_xpk: f32,
    mt_split_k: bool,
    mt_q: [usize; 8],
    mt_qsplit: [bool; 8],
    mt_nq: usize,
}

thread_local! {
    static PAIR: core::cell::UnsafeCell<Box<PairScratch>> = core::cell::UnsafeCell::new(Box::new(PairScratch {
        ec_n: [0; MAX_EFF],
        ec_c: [0.0; MAX_EFF * MAX_CAND],
        ec_mx: [0.0; MAX_EFF * MAX_CAND],
        ec_my: [0.0; MAX_EFF * MAX_CAND],
        ec_ex: [0.0; MAX_EFF * MAX_CAND],
        ec_ey: [0.0; MAX_EFF * MAX_CAND],
        ec_ez: [0.0; MAX_EFF * MAX_CAND],
        ec_reg: [0.0; MAX_EFF * MAX_CAND],
        ec_gi: [0; MAX_EFF * MAX_CAND],
        mt_ok: [false; 2 * MAX_EFF],
        mt_s: [0.0; 2 * MAX_EFF * MAX_CAND],
        mt_t1: [0.0; 2 * MAX_EFF * MAX_CAND],
        mt_t2: [0.0; 2 * MAX_EFF * MAX_CAND],
        mt_t3: [0.0; 2 * MAX_EFF * MAX_CAND],
        mt_d: [0.0; 2 * MAX_EFF * MAX_CAND],
        mv_all: [V3 { x: 0.0, y: 0.0, z: 0.0 }; MAX_EFF],
        rt_all: [0.0; MAX_EFF],
        py_all: [0.0; MAX_EFF],
        y0a: [0.0; MAX_EFF],
        base_g: [0.0; FIGHT_GROUPS + 1],
        base_p1: 0.0,
        base_p2: 0.0,
        e0x: 0.0,
        e0y: 0.0,
        e0z: 0.0,
        jp: -1,
        jp2: -1,
        js: -1,
        b_a: 0,
        b_b: 0,
        o_a: 0,
        o_b: 0,
        t_a: 0,
        t_b: 0,
        mt_pk: -1,
        mt_xpk: 0.0,
        mt_split_k: false,
        mt_q: [0; 8],
        mt_qsplit: [false; 8],
        mt_nq: 0,
    }));
}

#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct V3 {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

impl V3 {
    pub const ZERO: V3 = V3 { x: 0.0, y: 0.0, z: 0.0 };

    // UnityEngine.Vector3: operator *(Vector3, float), operator /(Vector3, float), Dot, sqrMagnitude
    #[inline(always)]
    fn mul(self, d: f32) -> V3 {
        V3 { x: self.x * d, y: self.y * d, z: self.z * d }
    }
    #[inline(always)]
    fn div(self, d: f32) -> V3 {
        V3 { x: self.x / d, y: self.y / d, z: self.z / d }
    }
    #[inline(always)]
    fn dot(a: V3, b: V3) -> f32 {
        a.x * b.x + a.y * b.y + a.z * b.z
    }
    #[inline(always)]
    fn sqr(self) -> f32 {
        self.x * self.x + self.y * self.y + self.z * self.z
    }
}

impl Add for V3 {
    type Output = V3;
    #[inline(always)]
    fn add(self, b: V3) -> V3 {
        V3 { x: self.x + b.x, y: self.y + b.y, z: self.z + b.z }
    }
}

impl Sub for V3 {
    type Output = V3;
    #[inline(always)]
    fn sub(self, b: V3) -> V3 {
        V3 { x: self.x - b.x, y: self.y - b.y, z: self.z - b.z }
    }
}

/// One effector, as `Allocator.Solve` reads it (780 bytes since 5.0.0, every field 4 bytes wide).
#[repr(C)]
#[derive(Clone, Copy)]
pub struct EffN {
    pub active: i32,
    /// 1 = TvcEffector, 2 = SurfaceEffector of kind Stabilator, 0 = any other effector
    pub kind: i32,
    /// index of `partner` in this array, -1 when it has none (or its partner is not in the list)
    pub partner: i32,
    pub roll_group: i32,
    pub pitch_group: i32,
    pub two_horizon: i32,
    pub n: i32,
    pub node_on: i32,
    pub node_k: i32,
    pub min: f32,
    pub max: f32,
    pub now: f32,
    pub cmd: f32,
    pub rate: f32,
    pub reach_tau: f32,
    pub mu: f32,
    pub lam: f32,
    pub lo: f32,
    pub hi: f32,
    pub pair_sign: f32,
    pub sym_cost: f32,
    pub sym_target: f32,
    pub anti_cost: f32,
    pub anchor: f32,
    pub band: f32,
    pub anchor_k: f32,
    /// Effector.RegN, evaluated by the plugin
    pub reg_n: f32,
    /// Effector.Step, evaluated by the plugin
    pub step: f32,
    pub node_c: f32,
    pub node_d: V3,
    pub grid: [f32; MAX_N],
    pub m: [V3; MAX_N],
    /// 5.0.0 (Allocator.PairBlocks): the pair search's class (1 stab, 2 wing trailing edge, 3 rudder, 4 nozzles, 0 other) and side
    /// (-1 left, +1 right, 0 none), and the true pitch curve (Effector.yTrue) when ytrue_on
    pub pcls: i32,
    pub side: i32,
    pub ytrue_on: i32,
    pub ytrue: [f32; MAX_N],
}

/// The allocator's configuration, the target, and what a solve leaves behind (228 bytes since 5.0.0).
#[repr(C)]
#[derive(Clone, Copy)]
pub struct AllocIo {
    /// Allocator.LmFactor
    pub lm_factor: f64,
    /// effs.Count
    pub n: i32,
    pub gn_iterations: i32,
    pub paired_trim_search: i32,
    pub fight_stab_split_only: i32,
    pub reach_pass: i32,
    pub two_horizon: i32,
    pub two_horizon_path: i32,
    pub coord_newton: i32,
    pub axis_w: V3,
    pub reg_scale: f32,
    pub fight_k: f32,
    pub fight_w: [f32; 4],
    pub pitch_fight_k: f32,
    pub pitch_fight_sign: f32,
    /// i00 i01 i02 i10 i11 i12 i20 i21 i22
    pub inv: [f32; 9],
    pub fight_target_min: f32,
    pub pair_horizon: f32,
    pub target: V3,
    /// in: the allocator's lastCost before the call; out: after it
    pub last_cost: f32,
    /// in / out: resDbg
    pub res_dbg: V3,
    /// in / out: lastNeededPick
    pub last_needed_pick: i32,
    /// out: how many times each counter was incremented by this solve
    pub reach_moves: i32,
    pub two_horizon_bound: i32,
    pub coord_newton_moves: i32,
    pub paired_moves: i32,
    /// out: SolveCore runs in this call (diagnostics)
    pub core_solves: i32,
    /// 5.0.0: Allocator.pairBlocks, PairBlockSet, PairTrueCurves, PairBlockPasses, PairBlockHorizon
    pub pair_blocks: i32,
    pub pair_set: i32,
    pub pair_true: i32,
    pub pair_passes: i32,
    pub pair_horizon_b: f32,
    /// out: this solve's pair-search moves, and by block kind
    pub pair_moves: i32,
    pub pair_moves_k: [i32; PAIR_KINDS],
    /// 5.1.0: Allocator.HorizonObjective, HorizonSteps, BranchStride, branchScan, HorizonT
    pub horizon_on: i32,
    pub horizon_steps: i32,
    pub branch_stride: i32,
    pub branch_scan: i32,
    pub horizon_t: f32,
    /// out: HorizonPick runs, branch refinements, branches taken
    pub horizon_picks: i32,
    pub branch_refines: i32,
    pub branch_taken: i32,
}

// ---- UnityEngine.Mathf, as the game's UnityEngine.CoreModule implements it
#[inline(always)]
fn maxf(a: f32, b: f32) -> f32 {
    if a > b {
        a
    } else {
        b
    }
}

#[inline(always)]
fn minf(a: f32, b: f32) -> f32 {
    if a < b {
        a
    } else {
        b
    }
}

#[inline(always)]
fn clampf(value: f32, min: f32, max: f32) -> f32 {
    if value < min {
        min
    } else if value > max {
        max
    } else {
        value
    }
}

#[inline(always)]
fn absf(f: f32) -> f32 {
    f.abs()
}

/// Mathf.Sqrt(f) = (float)Math.Sqrt((double)f)
#[inline(always)]
fn sqrtf(f: f32) -> f32 {
    (f as f64).sqrt() as f32
}

/// C# `(int)float` on x64 (cvttss2si): truncation, and 0x80000000 for NaN or out of range, where Rust's `as` would
/// saturate. An index built from that then fails its bounds check here just as it throws in C#.
#[inline(always)]
fn f2i(f: f32) -> i32 {
    #[cfg(target_arch = "x86_64")]
    unsafe {
        use core::arch::x86_64::{_mm_cvttss_si32, _mm_set_ss};
        _mm_cvttss_si32(_mm_set_ss(f))
    }
    #[cfg(not(target_arch = "x86_64"))]
    {
        if f.is_nan() || f >= 2147483648.0 || f < -2147483648.0 {
            i32::MIN
        } else {
            f as i32
        }
    }
}

#[inline(always)]
fn ix(i: i32) -> usize {
    // a negative index wraps to a huge one and fails the bounds check (C#: IndexOutOfRangeException)
    i as u32 as usize
}

/// Effector.MAt
#[inline(always)]
fn m_at(e: &EffN, c: f32) -> V3 {
    if e.n < 2 {
        return V3::ZERO;
    }
    let num = (c - e.min) / e.step;
    if num <= 0.0 {
        return e.m[0];
    }
    if num >= (e.n - 1) as f32 {
        return e.m[ix(e.n - 1)];
    }
    let num2 = f2i(num);
    let num3 = num - num2 as f32;
    let a = e.m[ix(num2)];
    let b = e.m[ix(num2 + 1)];
    let mut m = a + (b - a).mul(num3);
    if e.node_on != 0 && num2 == e.node_k {
        let g0 = e.min + num2 as f32 * e.step;
        let w = if c <= e.node_c {
            if e.node_c > g0 {
                (c - g0) / (e.node_c - g0)
            } else {
                1.0
            }
        } else if g0 + e.step > e.node_c {
            (g0 + e.step - c) / (g0 + e.step - e.node_c)
        } else {
            1.0
        };
        m = m + e.node_d.mul(w);
    }
    m
}

/// Allocator.BudgetExcess
#[inline(always)]
fn budget_excess(e: &EffN, c: f32) -> f32 {
    let d = c - e.anchor;
    if d > e.band {
        return d - e.band;
    }
    if d < 0.0 - e.band {
        return d + e.band;
    }
    0.0
}

/// Allocator.BudgetCost
#[inline(always)]
fn budget_cost(e: &EffN, c: f32) -> f32 {
    if e.anchor_k <= 0.0 {
        return 0.0;
    }
    let ex = budget_excess(e, c) / e.reg_n;
    e.anchor_k * ex * ex
}

const FIGHT_GROUPS: usize = 3;

struct Solver<'a> {
    io: &'a mut AllocIo,
    e: &'a mut [EffN],
    sw: V3,           // Mathf.Sqrt(axisWeight.*)
    sq_pfk: f64,      // Math.Sqrt(pitchFightK)
    sq_fk: [f64; 4],  // Math.Sqrt(fightK * fightW[g])
    hint: f64,        // fightSignHint
    cnt: usize,
    act: [usize; MAX_EFF],
    x: [f64; MAX_EFF],
    prev: [f64; MAX_EFF],
    dx: [f64; MAX_EFF],
    h: [[f64; MAX_EFF]; MAX_EFF],
    rhs: [f64; MAX_EFF],
    jc: [V3; MAX_EFF],
    pidx: [i32; MAX_EFF],
    r0: [f64; MAX_EFF],
    grp: [i32; MAX_EFF],
    jg: [f64; MAX_EFF],
    pgrp: [i32; MAX_EFF],
    y0: [f64; MAX_EFF],
    jp: [f64; MAX_EFF],
    pg_r: [f64; 3],
    pg_on: [bool; 3],
    gr_r: [f64; FIGHT_GROUPS + 1],
    gr_on: [bool; FIGHT_GROUPS + 1],
    // the reach pass
    rch_n: usize,
    rch_e: [usize; MAX_EFF],
    rch_dest: [f32; MAX_EFF],
    rch_lo: [f32; MAX_EFF],
    rch_hi: [f32; MAX_EFF],
    rch_cmd0: [f32; MAX_EFF],
    // 5.0.0: the pair search's blocks
    n_blk: usize,
    blk_a: [usize; MAX_BLOCKS],
    blk_b: [usize; MAX_BLOCKS],
    blk_k: [usize; MAX_BLOCKS],
    // 5.1.0: the horizon objective's linear answer
    ln_n: usize,
    ln_g: [V3; MAX_EFF],
    ln_c: [f32; MAX_EFF],
    ln_lo: [f32; MAX_EFF],
    ln_hi: [f32; MAX_EFF],
    ln_m: [f32; 9],
}

impl<'a> Solver<'a> {
    fn new(io: &'a mut AllocIo, e: &'a mut [EffN]) -> Self {
        let sw = V3 { x: sqrtf(io.axis_w.x), y: sqrtf(io.axis_w.y), z: sqrtf(io.axis_w.z) };
        let sq_pfk = (io.pitch_fight_k as f64).sqrt();
        let mut sq_fk = [0.0f64; 4];
        for g in 0..4 {
            sq_fk[g] = ((io.fight_k * io.fight_w[g]) as f64).sqrt();
        }
        Solver {
            io,
            e,
            sw,
            sq_pfk,
            sq_fk,
            hint: 0.0,
            cnt: 0,
            act: [0; MAX_EFF],
            x: [0.0; MAX_EFF],
            prev: [0.0; MAX_EFF],
            dx: [0.0; MAX_EFF],
            h: [[0.0; MAX_EFF]; MAX_EFF],
            rhs: [0.0; MAX_EFF],
            jc: [V3::ZERO; MAX_EFF],
            pidx: [-1; MAX_EFF],
            r0: [0.0; MAX_EFF],
            grp: [0; MAX_EFF],
            jg: [0.0; MAX_EFF],
            pgrp: [0; MAX_EFF],
            y0: [0.0; MAX_EFF],
            jp: [0.0; MAX_EFF],
            pg_r: [0.0; 3],
            pg_on: [false; 3],
            gr_r: [0.0; FIGHT_GROUPS + 1],
            gr_on: [false; FIGHT_GROUPS + 1],
            rch_n: 0,
            rch_e: [0; MAX_EFF],
            rch_dest: [0.0; MAX_EFF],
            rch_lo: [0.0; MAX_EFF],
            rch_hi: [0.0; MAX_EFF],
            rch_cmd0: [0.0; MAX_EFF],
            n_blk: 0,
            blk_a: [0; MAX_BLOCKS],
            blk_b: [0; MAX_BLOCKS],
            blk_k: [0; MAX_BLOCKS],
            ln_n: 0,
            ln_g: [V3::ZERO; MAX_EFF],
            ln_c: [0.0; MAX_EFF],
            ln_lo: [0.0; MAX_EFF],
            ln_hi: [0.0; MAX_EFF],
            ln_m: [0.0; 9],
        }
    }

    #[inline(always)]
    fn ae(&self, i: usize) -> &EffN {
        &self.e[self.act[i]]
    }

    /// act[i].MAt(c)
    #[inline(always)]
    fn mat(&self, i: usize, c: f32) -> V3 {
        m_at(&self.e[self.act[i]], c)
    }

    /// Allocator.E
    #[inline(always)]
    fn E(&self, m: V3) -> V3 {
        let i = &self.io.inv;
        let num = i[0] * m.x + i[1] * m.y + i[2] * m.z;
        let num2 = i[3] * m.x + i[4] * m.y + i[5] * m.z;
        let z = (i[6] * m.x + i[7] * m.y + i[8] * m.z) * self.sw.z;
        V3 { x: num * self.sw.x, y: num2 * self.sw.y, z }
    }

    /// Allocator.Cost
    #[inline(always)]
    fn cost(&self, err: V3) -> f32 {
        self.E(err).sqr()
    }

    /// Allocator.Reg
    #[inline(always)]
    fn reg(&self, e: &EffN, c: f32, p: f32) -> f32 {
        let num = e.reg_n;
        let num2 = c / num;
        let num3 = (c - p) / num;
        self.io.reg_scale * (e.mu * num2 * num2 + e.lam * num3 * num3) + budget_cost(e, c)
    }

    /// Allocator.PairReg
    #[inline(always)]
    fn pair_reg(&self, i: usize) -> f32 {
        let num = self.pidx[i];
        if num < 0 {
            return 0.0;
        }
        let num = num as usize;
        let effector = self.ae(i);
        let effector2 = self.ae(num);
        let num2 = effector.reg_n;
        let num3 = effector.pair_sign * (self.x[i] as f32) / num2;
        let num4 = effector2.pair_sign * (self.x[num] as f32) / num2;
        let num5 = 0.5f32 * (num3 - num4) - 0.5f32 * (effector.pair_sign - effector2.pair_sign) * effector.sym_target / num2;
        let num6 = 0.5f32 * (num3 + num4);
        self.io.reg_scale * (effector.sym_cost * num5 * num5 + effector.anti_cost * num6 * num6)
    }

    /// Allocator.PairRegWith
    #[inline(always)]
    fn pair_reg_with(&mut self, j: usize, c: f32) -> f32 {
        let num = self.x[j];
        self.x[j] = c as f64;
        let mut num2 = 0.0f32;
        for i in 0..self.cnt {
            if self.pidx[i] >= 0 && (i == j || self.pidx[i] as usize == j) {
                num2 += self.pair_reg(i);
            }
        }
        self.x[j] = num;
        num2
    }

    /// Allocator.StabSplitMode
    #[inline(always)]
    fn stab_split_mode(&self, i: usize) -> bool {
        self.io.fight_stab_split_only != 0 && self.grp[i] == 1 && self.pidx[i] >= 0 && self.grp[self.pidx[i] as usize] == 1
    }

    /// Allocator.GroupRoll
    fn group_roll(&self, g: i32) -> f64 {
        let i00 = self.io.inv[0] as f64;
        let mut s = 0.0f64;
        for i in 0..self.cnt {
            if self.grp[i] == g {
                if self.stab_split_mode(i) {
                    let j = self.pidx[i] as usize;
                    let sym = 0.5 * (self.ae(i).pair_sign as f64 * self.x[i] - self.ae(j).pair_sign as f64 * self.x[j]);
                    let xs = (self.ae(i).pair_sign as f64 * sym) as f32;
                    s += i00 * (self.mat(i, self.x[i] as f32).x as f64 - self.mat(i, xs).x as f64);
                    continue;
                }
                s += i00 * (self.mat(i, self.x[i] as f32).x as f64 - self.r0[i]);
            }
        }
        s
    }

    /// Allocator.NetSign
    fn net_sign(&self) -> f64 {
        if self.hint != 0.0 {
            return self.hint;
        }
        let mut t = 0.0f64;
        for g in 1..=FIGHT_GROUPS as i32 {
            t += self.group_roll(g);
        }
        if t >= 0.0 {
            1.0
        } else {
            -1.0
        }
    }

    /// Allocator.FightCost
    fn fight_cost(&self) -> f32 {
        if self.io.fight_k <= 0.0 {
            return 0.0;
        }
        let sg = self.net_sign();
        let mut t = 0.0f64;
        for g in 1..=FIGHT_GROUPS {
            let a = self.group_roll(g as i32);
            if a * sg < 0.0 {
                t += self.io.fight_w[g] as f64 * a * a;
            }
        }
        (self.io.fight_k as f64 * t) as f32
    }

    /// Allocator.FightCostWith
    fn fight_cost_with(&mut self, j: usize, c: f32) -> f32 {
        if self.io.fight_k <= 0.0 || self.grp[j] == 0 {
            return 0.0;
        }
        let i00 = self.io.inv[0] as f64;
        let d = i00 * (self.mat(j, c).x as f64 - self.mat(j, self.x[j] as f32).x as f64);
        let mut tot = 0.0f64;
        let split_j = self.stab_split_mode(j);
        let xj = self.x[j];
        if split_j {
            self.x[j] = c as f64;
        }
        for g in 1..=FIGHT_GROUPS {
            let v = self.group_roll(g as i32) + if g as i32 == self.grp[j] && !split_j { d } else { 0.0 };
            self.gr_r[g] = v;
            tot += self.gr_r[g];
        }
        self.x[j] = xj;
        let sg = if self.hint != 0.0 {
            self.hint
        } else if tot >= 0.0 {
            1.0
        } else {
            -1.0
        };
        let mut t = 0.0f64;
        for g in 1..=FIGHT_GROUPS {
            if self.gr_r[g] * sg < 0.0 {
                t += self.io.fight_w[g] as f64 * self.gr_r[g] * self.gr_r[g];
            }
        }
        (self.io.fight_k as f64 * t) as f32
    }

    /// Allocator.GroupPitch
    fn group_pitch(&self, g: i32) -> f64 {
        let i11 = self.io.inv[4] as f64;
        let mut s = 0.0f64;
        for i in 0..self.cnt {
            if self.pgrp[i] == g {
                s += i11 * (self.mat(i, self.x[i] as f32).y as f64 - self.y0[i]);
            }
        }
        s
    }

    /// Allocator.PFTerm
    #[inline(always)]
    fn pf_term(&self, a: f64, b: f64) -> f64 {
        let k = self.io.pitch_fight_sign as f64;
        if a * k <= 0.0 || b * k >= 0.0 {
            return 0.0;
        }
        let sg = if a + b >= 0.0 { 1.0 } else { -1.0 };
        (if a * sg < 0.0 { a * a } else { 0.0 }) + (if b * sg < 0.0 { b * b } else { 0.0 })
    }

    /// Allocator.PitchFightCost
    fn pitch_fight_cost(&self) -> f32 {
        if self.io.pitch_fight_k <= 0.0 {
            return 0.0;
        }
        (self.io.pitch_fight_k as f64 * self.pf_term(self.group_pitch(1), self.group_pitch(2))) as f32
    }

    /// Allocator.PFPair
    fn pf_pair(&mut self, s: usize, cs: f32, t: usize, ct: f32) -> f32 {
        if self.io.pitch_fight_k <= 0.0 {
            return 0.0;
        }
        let xs = self.x[s];
        let xt = self.x[t];
        self.x[s] = cs as f64;
        self.x[t] = ct as f64;
        let r = self.pitch_fight_cost();
        self.x[s] = xs;
        self.x[t] = xt;
        r
    }

    /// Allocator.PitchFightCostWith
    fn pitch_fight_cost_with(&self, j: usize, c: f32) -> f32 {
        if self.io.pitch_fight_k <= 0.0 || self.pgrp[j] == 0 {
            return 0.0;
        }
        let i11 = self.io.inv[4] as f64;
        let d = i11 * (self.mat(j, c).y as f64 - self.mat(j, self.x[j] as f32).y as f64);
        let a = self.group_pitch(1) + if self.pgrp[j] == 1 { d } else { 0.0 };
        let b = self.group_pitch(2) + if self.pgrp[j] == 2 { d } else { 0.0 };
        (self.io.pitch_fight_k as f64 * self.pf_term(a, b)) as f32
    }

    /// Allocator.Total
    fn total(&self) -> f32 {
        let mut zero = V3::ZERO;
        let mut num = 0.0f32;
        for i in 0..self.cnt {
            zero = zero + self.mat(i, self.x[i] as f32);
            num += self.reg(self.ae(i), self.x[i] as f32, self.prev[i] as f32) + self.pair_reg(i);
        }
        self.cost(zero - self.io.target) + num + self.fight_cost() + self.pitch_fight_cost()
    }

    /// Allocator.MomentSum
    fn moment_sum(&self) -> V3 {
        let mut t = V3::ZERO;
        for i in 0..self.cnt {
            t = t + self.mat(i, self.x[i] as f32);
        }
        t
    }

    /// the five-term cost of effector j at deflection c that the scan and the Newton candidate compare:
    /// Cost(m - vector4) + Reg + PairRegWith + FightCostWith + PitchFightCostWith
    #[inline(always)]
    fn cand(&mut self, j: usize, m: V3, vector4: V3, c: f32, p: f32) -> f32 {
        let a = self.cost(m - vector4);
        let b = self.reg(self.ae(j), c, p);
        let cc = self.pair_reg_with(j, c);
        let d = self.fight_cost_with(j, c);
        let ee = self.pitch_fight_cost_with(j, c);
        a + b + cc + d + ee
    }

    /// Allocator.SolveCore
    fn solve_core(&mut self) {
        self.io.core_solves += 1;
        let n_all = self.e.len();
        let mut count = 0usize;
        for i in 0..n_all {
            if self.e[i].active != 0 {
                self.act[count] = i;
                count += 1;
            }
        }
        self.cnt = count;
        if count == 0 {
            return;
        }
        for i in 0..count {
            let ef = self.ae(i);
            let xi = clampf(ef.cmd, ef.lo, ef.hi) as f64;
            let pi = ef.cmd as f64;
            self.x[i] = xi;
            self.prev[i] = pi;
        }
        let fight_k = self.io.fight_k;
        let pitch_fight_k = self.io.pitch_fight_k;
        for j in 0..count {
            let ef = *self.ae(j);
            // act.IndexOf(act[j].partner)
            let mut pi = -1i32;
            if ef.partner >= 0 {
                let p = ef.partner as usize;
                for k in 0..count {
                    if self.act[k] == p {
                        pi = k as i32;
                        break;
                    }
                }
            }
            self.pidx[j] = pi;
            self.grp[j] = if fight_k > 0.0 { ef.roll_group } else { 0 };
            self.r0[j] = m_at(&ef, 0.0).x as f64;
            self.pgrp[j] = if pitch_fight_k > 0.0 { ef.pitch_group } else { 0 };
            self.y0[j] = m_at(&ef, 0.0).y as f64;
        }
        self.n_blk = 0;
        if self.io.pair_blocks != 0 {
            self.build_blocks(count);
        }
        let i00 = self.io.inv[0] as f64;
        let i11 = self.io.inv[4] as f64;
        let target = self.io.target;
        self.hint = 0.0;
        if fight_k > 0.0 && self.io.fight_target_min > 0.0 {
            let mut tr = i00 * target.x as f64;
            for jh in 0..count {
                tr -= i00 * self.r0[jh];
            }
            if (tr * 57.29578f64).abs() > self.io.fight_target_min as f64 {
                self.hint = if tr >= 0.0 { 1.0 } else { -1.0 };
            }
        }
        let reg_scale = self.io.reg_scale;
        let lm_factor = self.io.lm_factor;
        let mut num = self.total();
        for _k in 0..self.io.gn_iterations {
            let mut zero = V3::ZERO;
            for l in 0..count {
                zero = zero + self.mat(l, self.x[l] as f32);
            }
            let vector = self.E(zero - target);
            for m in 0..count {
                let effector = *self.ae(m);
                let num2 = 0.5f32 * effector.step;
                let num3 = maxf(effector.min, self.x[m] as f32 - num2);
                let num4 = minf(effector.max, self.x[m] as f32 + num2);
                self.jc[m] = if num4 > num3 {
                    self.E(m_at(&effector, num4) - m_at(&effector, num3)).div(num4 - num3)
                } else {
                    V3::ZERO
                };
                self.jp[m] = if self.pgrp[m] != 0 && num4 > num3 {
                    self.sq_pfk * i11 * ((m_at(&effector, num4).y - m_at(&effector, num3).y) as f64) / ((num4 - num3) as f64)
                } else {
                    0.0
                };
                self.jg[m] = if self.grp[m] != 0 && num4 > num3 {
                    self.sq_fk[ix(self.grp[m])] * i00 * ((m_at(&effector, num4).x - m_at(&effector, num3).x) as f64) / ((num4 - num3) as f64)
                } else {
                    0.0
                };
                if self.grp[m] != 0 && num4 > num3 && self.stab_split_mode(m) {
                    let xm = self.x[m];
                    self.x[m] = num4 as f64;
                    let g4 = self.group_roll(self.grp[m]);
                    self.x[m] = num3 as f64;
                    let g3 = self.group_roll(self.grp[m]);
                    self.x[m] = xm;
                    self.jg[m] = self.sq_fk[ix(self.grp[m])] * (g4 - g3) / ((num4 - num3) as f64);
                }
            }
            if pitch_fight_k > 0.0 {
                let pa = self.group_pitch(1);
                let pb = self.group_pitch(2);
                let sg_p = if pa + pb >= 0.0 { 1.0 } else { -1.0 };
                let pfs = self.io.pitch_fight_sign as f64;
                let pat = pa * pfs > 0.0 && pb * pfs < 0.0;
                self.pg_on[1] = pat && pa * sg_p < 0.0;
                self.pg_on[2] = pat && pb * sg_p < 0.0;
                self.pg_r[1] = if self.pg_on[1] { self.sq_pfk * pa } else { 0.0 };
                self.pg_r[2] = if self.pg_on[2] { self.sq_pfk * pb } else { 0.0 };
            } else {
                self.pg_on[1] = false;
                self.pg_on[2] = false;
            }
            if fight_k > 0.0 {
                let sg_f = self.net_sign();
                for g in 1..=FIGHT_GROUPS {
                    let gr = self.group_roll(g as i32);
                    self.gr_on[g] = gr * sg_f < 0.0;
                    self.gr_r[g] = if self.gr_on[g] { self.sq_fk[g] * gr } else { 0.0 };
                }
            }
            for n in 0..count {
                let effector2 = *self.ae(n);
                let num5 = effector2.reg_n;
                let num6 = 1.0 / ((num5 * num5) as f64);
                for num7 in 0..count {
                    self.h[n][num7] = V3::dot(self.jc[n], self.jc[num7]) as f64;
                    if self.pgrp[n] != 0 && self.pgrp[n] == self.pgrp[num7] && self.pg_on[ix(self.pgrp[n])] {
                        self.h[n][num7] += self.jp[n] * self.jp[num7];
                    }
                    if self.grp[n] != 0 && self.grp[n] == self.grp[num7] && self.gr_on[ix(self.grp[n])] {
                        self.h[n][num7] += self.jg[n] * self.jg[num7];
                    }
                }
                let num8 = lm_factor * self.h[n][n] + 1E-09f64;
                let num9 = reg_scale as f64;
                self.h[n][n] += num9 * ((effector2.mu + effector2.lam) as f64) * num6 + num8;
                self.rhs[n] = 0.0
                    - (V3::dot(self.jc[n], vector) as f64
                        + num9 * (effector2.mu as f64 * num6 * self.x[n] + effector2.lam as f64 * num6 * (self.x[n] - self.prev[n])));
                if self.grp[n] != 0 && self.gr_on[ix(self.grp[n])] {
                    self.rhs[n] -= self.jg[n] * self.gr_r[ix(self.grp[n])];
                }
                if self.pgrp[n] != 0 && self.pg_on[ix(self.pgrp[n])] {
                    self.rhs[n] -= self.jp[n] * self.pg_r[ix(self.pgrp[n])];
                }
                if effector2.anchor_k > 0.0 {
                    let bx = budget_excess(&effector2, self.x[n] as f32);
                    if bx != 0.0 {
                        self.h[n][n] += effector2.anchor_k as f64 * num6;
                        self.rhs[n] -= effector2.anchor_k as f64 * num6 * bx as f64;
                    }
                }
            }
            for num10 in 0..count {
                let num11 = self.pidx[num10];
                if num11 >= 0 {
                    let num11 = num11 as usize;
                    let effector3 = *self.ae(num10);
                    let effector4 = *self.ae(num11);
                    let num12 = effector3.reg_n;
                    let num13 = 1.0 / ((num12 * num12) as f64);
                    let num14 = effector3.pair_sign as f64;
                    let num15 = effector4.pair_sign as f64;
                    let num16 = 0.25 * reg_scale as f64 * effector3.sym_cost as f64 * num13;
                    let num17 = 0.25 * reg_scale as f64 * effector3.anti_cost as f64 * num13;
                    let num18 = num14 * self.x[num10] - num15 * self.x[num11] - (num14 - num15) * effector3.sym_target as f64;
                    let num19 = num14 * self.x[num10] + num15 * self.x[num11];
                    self.h[num10][num10] += num16 + num17;
                    self.h[num11][num11] += num16 + num17;
                    self.h[num10][num11] += (0.0 - num16 + num17) * num14 * num15;
                    self.h[num11][num10] += (0.0 - num16 + num17) * num14 * num15;
                    self.rhs[num10] -= num16 * num14 * num18 + num17 * num14 * num19;
                    self.rhs[num11] -= (0.0 - num16) * num15 * num18 + num17 * num15 * num19;
                }
            }
            if !solve_linear(&self.h, &self.rhs, &mut self.dx, count) {
                break;
            }
            let mut num20 = 1.0f64;
            let mut flag = false;
            let mut array = [0.0f64; MAX_EFF];
            let mut num21 = 0;
            while num21 < 4 && !flag {
                for num22 in 0..count {
                    array[num22] = self.x[num22];
                }
                for num23 in 0..count {
                    let ef = self.ae(num23);
                    let v = clampf((array[num23] + num20 * self.dx[num23]) as f32, ef.lo, ef.hi);
                    self.x[num23] = v as f64;
                }
                let num24 = self.total();
                if num24 < num {
                    num = num24;
                    flag = true;
                } else {
                    for num25 in 0..count {
                        self.x[num25] = array[num25];
                    }
                }
                num21 += 1;
                num20 *= 0.5;
            }
            if !flag {
                break;
            }
        }
        let mut vector2 = V3::ZERO;
        for num26 in 0..count {
            vector2 = vector2 + self.mat(num26, self.x[num26] as f32);
        }
        let coord_newton = self.io.coord_newton;
        for num27 in 0..count {
            let effector5 = *self.ae(num27);
            let num28 = self.x[num27] as f32;
            let p = self.prev[num27] as f32;
            let vector3 = vector2 - m_at(&effector5, num28);
            let vector4 = target - vector3;
            let mut num29 = self.cand(num27, m_at(&effector5, num28), vector4, num28, p);
            let mut num30 = num28;
            let mut num31 = -1i32;
            for num32 in 0..effector5.n {
                let num33 = effector5.grid[ix(num32)];
                if !(num33 < effector5.lo - 0.0001f32) && !(num33 > effector5.hi + 0.0001f32) {
                    let num34 = self.cand(num27, effector5.m[ix(num32)], vector4, num33, p);
                    if num34 < num29 - 1E-09f32 {
                        num29 = num34;
                        num30 = num33;
                        num31 = num32;
                    }
                }
            }
            if num31 > 0 && num31 < effector5.n - 1 {
                let c = effector5.grid[ix(num31 - 1)];
                let c2 = effector5.grid[ix(num31 + 1)];
                let num35 = self.cand(num27, effector5.m[ix(num31 - 1)], vector4, c, p);
                let num36 = self.cand(num27, effector5.m[ix(num31 + 1)], vector4, c2, p);
                let num37 = num35 - 2.0f32 * num29 + num36;
                if num37 > 1E-12f32 {
                    let num38 = clampf(
                        effector5.grid[ix(num31)] + clampf(0.5f32 * (num35 - num36) / num37, -0.5f32, 0.5f32) * effector5.step,
                        effector5.lo,
                        effector5.hi,
                    );
                    let num39 = self.cand(num27, m_at(&effector5, num38), vector4, num38, p);
                    if num39 < num29 {
                        num29 = num39;
                        num30 = num38;
                    }
                }
            }
            if coord_newton == 2 || (coord_newton == 1 && effector5.kind == 1) {
                let xc = self.x[num27] as f32;
                let h_n = 0.25f32 * effector5.step;
                let a_n = maxf(effector5.lo, xc - h_n);
                let b_n = minf(effector5.hi, xc + h_n);
                if b_n > a_n + 1E-05f32 {
                    let g_n = self.E(m_at(&effector5, b_n) - m_at(&effector5, a_n)).div(b_n - a_n);
                    let r_n = self.E(m_at(&effector5, xc) - vector4);
                    let rn2 = effector5.reg_n * effector5.reg_n;
                    let den_n = V3::dot(g_n, g_n) as f64 + (reg_scale * (effector5.mu + effector5.lam) / rn2) as f64;
                    if den_n > 1E-12f64 {
                        let gr_n = V3::dot(g_n, r_n) as f64 + (reg_scale * (effector5.mu * xc + effector5.lam * (xc - p)) / rn2) as f64;
                        let c_n = clampf((xc as f64 - gr_n / den_n) as f32, effector5.lo, effector5.hi);
                        let cost_n = self.cand(num27, m_at(&effector5, c_n), vector4, c_n, p);
                        if cost_n < num29 {
                            #[allow(unused_assignments)]
                            {
                                num29 = cost_n;
                            }
                            num30 = c_n;
                            self.io.coord_newton_moves += 1;
                        }
                    }
                }
            }
            self.x[num27] = num30 as f64;
            vector2 = vector3 + m_at(&effector5, num30);
        }
        if self.io.paired_trim_search != 0 {
            self.paired_search(&mut vector2);
        }
        if self.io.pair_blocks != 0 && self.n_blk > 0 {
            PAIR.with(|c| {
                let ps = unsafe { &mut *c.get() };
                self.pair_block_search(ps);
            });
        }
        self.io.res_dbg = self.moment_sum() - target;
        for num40 in 0..count {
            let k = self.act[num40];
            let c = clampf(self.x[num40] as f32, self.e[k].lo, self.e[k].hi);
            self.e[k].cmd = c;
            self.x[num40] = c as f64;
        }
        self.io.last_cost = self.total();
    }

    // ---- F-22E 5.0.0: the pair search (Allocator.cs, "pair-complete allocator"), line for line

    /// Allocator.AddBlock
    fn add_block(&mut self, a: usize, b: usize, k: usize) {
        if self.n_blk < MAX_BLOCKS {
            self.blk_a[self.n_blk] = a;
            self.blk_b[self.n_blk] = b;
            self.blk_k[self.n_blk] = k;
            self.n_blk += 1;
        }
    }

    /// Allocator.BuildBlocks
    fn build_blocks(&mut self, count: usize) {
        self.n_blk = 0;
        for i in 0..count {
            let p = self.pidx[i];
            if self.ae(i).pcls == 1 && p >= 0 && self.ae(p as usize).pcls == 1 {
                self.add_block(i, p as usize, 0);
            }
        }
        if self.io.pair_set < 2 {
            return;
        }
        for i in 0..count {
            let p = self.pidx[i];
            if self.ae(i).pcls == 2 && p >= 0 && self.ae(p as usize).pcls == 2 {
                self.add_block(i, p as usize, 1);
            }
        }
        for i in 0..count {
            let p = self.pidx[i];
            if self.ae(i).pcls == 3 && p >= 0 && self.ae(p as usize).pcls == 3 {
                self.add_block(i, p as usize, 2);
            }
        }
        for i in 0..count {
            if self.ae(i).pcls != 1 {
                continue;
            }
            for t in 0..count {
                if self.ae(t).pcls == 4 {
                    self.add_block(i, t, 3);
                }
            }
        }
        for i in 0..count {
            if self.ae(i).pcls != 1 || self.ae(i).side == 0 {
                continue;
            }
            for j in 0..count {
                if self.ae(j).pcls == 2 && self.ae(j).side == self.ae(i).side {
                    self.add_block(i, j, 4);
                }
            }
        }
    }

    /// Allocator.MA
    #[inline(always)]
    fn ma(&self, i: usize, c: f32) -> V3 {
        let e = self.ae(i);
        let mut m = m_at(e, c);
        if self.io.pair_true != 0 && e.ytrue_on != 0 && e.n >= 2 {
            let y = &e.ytrue;
            let num = (c - e.min) / e.step;
            if num <= 0.0 {
                m.y = y[0];
            } else if num >= (e.n - 1) as f32 {
                m.y = y[ix(e.n - 1)];
            } else {
                let k = f2i(num);
                let f = num - k as f32;
                m.y = y[ix(k)] + (y[ix(k + 1)] - y[ix(k)]) * f;
            }
        }
        m
    }

    /// Allocator.SplitTerm
    #[inline(always)]
    fn split_term(&self, i: usize, ci: f32, mxi: f32, cp: f32) -> f64 {
        let j = self.pidx[i] as usize;
        let sym = 0.5f64 * (self.ae(i).pair_sign as f64 * ci as f64 - self.ae(j).pair_sign as f64 * cp as f64);
        let xs = (self.ae(i).pair_sign as f64 * sym) as f32;
        self.io.inv[0] as f64 * (mxi as f64 - self.mat(i, xs).x as f64)
    }

    /// Allocator.RollTerm
    fn roll_term(&self, i: usize) -> f64 {
        if self.stab_split_mode(i) {
            let j = self.pidx[i] as usize;
            return self.split_term(i, self.x[i] as f32, self.mat(i, self.x[i] as f32).x, self.x[j] as f32);
        }
        self.io.inv[0] as f64 * (self.mat(i, self.x[i] as f32).x as f64 - self.r0[i])
    }

    /// Allocator.PairRegAt
    #[inline(always)]
    fn pair_reg_at(&self, i: usize, ci: f32, cp: f32) -> f32 {
        let e = self.ae(i);
        let e2 = self.ae(self.pidx[i] as usize);
        let n2 = e.reg_n;
        let n3 = e.pair_sign * ci / n2;
        let n4 = e2.pair_sign * cp / n2;
        let n5 = 0.5f32 * (n3 - n4) - 0.5f32 * (e.pair_sign - e2.pair_sign) * e.sym_target / n2;
        let n6 = 0.5f32 * (n3 + n4);
        self.io.reg_scale * (e.sym_cost * n5 * n5 + e.anti_cost * n6 * n6)
    }

    /// Allocator.PairRelations
    fn pair_relations(&self, k: usize) -> i32 {
        let mut n = if self.pidx[k] >= 0 { 1 } else { 0 };
        for i in 0..self.cnt {
            if self.pidx[i] == k as i32 {
                n += 1;
            }
        }
        n
    }

    /// Allocator.PairCacheOne
    fn pair_cache_one(&self, ps: &mut PairScratch, i: usize) {
        ps.mv_all[i] = self.ma(i, self.x[i] as f32);
        ps.rt_all[i] = if self.grp[i] != 0 { self.roll_term(i) } else { 0.0 };
        ps.py_all[i] = if self.pgrp[i] != 0 { self.io.inv[4] as f64 * (ps.mv_all[i].y as f64 - ps.y0a[i]) } else { 0.0 };
    }

    /// Allocator.EcPut
    #[inline(always)]
    fn ec_put(&self, ps: &mut PairScratch, e: &EffN, o: usize, c: f32, g: i32, m: V3, p: f32) {
        let i = &self.io.inv;
        let num = i[0] * m.x + i[1] * m.y + i[2] * m.z;
        let num2 = i[3] * m.x + i[4] * m.y + i[5] * m.z;
        let num3 = i[6] * m.x + i[7] * m.y + i[8] * m.z;
        ps.ec_c[o] = c;
        ps.ec_mx[o] = m.x;
        ps.ec_my[o] = m.y;
        ps.ec_ex[o] = num * self.sw.x;
        ps.ec_ey[o] = num2 * self.sw.y;
        ps.ec_ez[o] = num3 * self.sw.z;
        ps.ec_reg[o] = self.reg(e, c, p);
        ps.ec_gi[o] = g;
    }

    /// Allocator.EcBuild
    fn ec_build(&self, ps: &mut PairScratch, k: usize) {
        let e = *self.ae(k);
        let p = self.prev[k] as f32;
        let x0 = self.x[k] as f32;
        let o = k * MAX_CAND;
        let m0 = self.ma(k, x0);
        self.ec_put(ps, &e, o, x0, -1, m0, p);
        let mut n = 1usize;
        let nn = e.n;
        let yt = self.io.pair_true != 0 && e.ytrue_on != 0 && nn >= 2;
        let lo = e.lo - 0.0001f32;
        let hi = e.hi + 0.0001f32;
        let now = e.now;
        let reach = maxf(e.step, self.io.pair_horizon_b * maxf(1.0f32, e.rate));
        let mut g = 0i32;
        while g < nn && n < TRIAL {
            let gs = e.grid[ix(g)];
            if gs < lo || gs > hi || absf(gs - now) > reach || gs == x0 {
                g += 1;
                continue;
            }
            let mut m = e.m[ix(g)];
            if yt {
                m.y = e.ytrue[ix(g)];
            }
            self.ec_put(ps, &e, o + n, gs, g, m, p);
            n += 1;
            g += 1;
        }
        ps.ec_n[k] = n as i32;
        ps.mt_ok[2 * k] = false;
        ps.mt_ok[2 * k + 1] = false;
    }

    /// Allocator.MtSetup
    fn mt_setup(&self, ps: &mut PairScratch, k: usize) {
        ps.mt_pk = self.pidx[k];
        ps.mt_xpk = if ps.mt_pk >= 0 { self.x[ps.mt_pk as usize] as f32 } else { 0.0 };
        ps.mt_split_k = self.grp[k] != 0 && self.stab_split_mode(k);
        ps.mt_nq = 0;
        let mut i = 0usize;
        while i < self.cnt && ps.mt_nq < 8 {
            if i != k && self.pidx[i] == k as i32 {
                ps.mt_q[ps.mt_nq] = i;
                ps.mt_qsplit[ps.mt_nq] = self.grp[i] != 0 && self.stab_split_mode(i);
                ps.mt_nq += 1;
            }
            i += 1;
        }
    }

    /// Allocator.MtPut
    fn mt_put(&self, ps: &mut PairScratch, k: usize, mode: i32, q: usize, t: usize) {
        let o = k * MAX_CAND + q;
        let c = ps.ec_c[o];
        let mut s = ps.ec_reg[o];
        if mode == 0 {
            if ps.mt_pk >= 0 {
                s += self.pair_reg_at(k, c, ps.mt_xpk);
            }
            for r in 0..ps.mt_nq {
                let i = ps.mt_q[r];
                s += self.pair_reg_at(i, self.x[i] as f32, c);
            }
        }
        ps.mt_s[t] = s;
        let mut t1 = 0.0f64;
        let mut t2 = 0.0f64;
        let mut t3 = 0.0f64;
        let gk = self.grp[k];
        if gk != 0 && !(mode == 1 && ps.mt_split_k) {
            let v = if ps.mt_split_k {
                self.split_term(k, c, ps.ec_mx[o], ps.mt_xpk)
            } else {
                self.io.inv[0] as f64 * (ps.ec_mx[o] as f64 - self.r0[k])
            };
            if gk == 1 {
                t1 += v;
            } else if gk == 2 {
                t2 += v;
            } else {
                t3 += v;
            }
        }
        if mode == 0 {
            for r in 0..ps.mt_nq {
                if !ps.mt_qsplit[r] {
                    continue;
                }
                let i = ps.mt_q[r];
                let v = self.split_term(i, self.x[i] as f32, ps.mv_all[i].x, c);
                let gi = self.grp[i];
                if gi == 1 {
                    t1 += v;
                } else if gi == 2 {
                    t2 += v;
                } else {
                    t3 += v;
                }
            }
        }
        ps.mt_t1[t] = t1;
        ps.mt_t2[t] = t2;
        ps.mt_t3[t] = t3;
        ps.mt_d[t] = if self.pgrp[k] != 0 { self.io.inv[4] as f64 * (ps.ec_my[o] as f64 - ps.y0a[k]) } else { 0.0 };
    }

    /// Allocator.MtBuild
    fn mt_build(&self, ps: &mut PairScratch, k: usize, mode: i32) {
        self.mt_setup(ps, k);
        let t = (2 * k + mode as usize) * MAX_CAND;
        let n = ps.ec_n[k] as usize;
        for q in 0..n {
            self.mt_put(ps, k, mode, q, t + q);
        }
        ps.mt_ok[2 * k + mode as usize] = true;
    }

    /// Allocator.Trial
    fn trial(&self, ps: &mut PairScratch, k: usize, mode: i32, c: f32) {
        let e = *self.ae(k);
        let m = self.ma(k, c);
        self.ec_put(ps, &e, k * MAX_CAND + TRIAL, c, -1, m, self.prev[k] as f32);
        self.mt_setup(ps, k);
        self.mt_put(ps, k, mode, TRIAL, (2 * k + mode as usize) * MAX_CAND + TRIAL);
    }

    /// Allocator.PairHead
    #[inline(always)]
    fn pair_head(&self, ps: &PairScratch, i: usize, j: usize) -> f32 {
        let ia = ps.o_a + i;
        let ib = ps.o_b + j;
        let vx = ps.e0x + ps.ec_ex[ia] + ps.ec_ex[ib];
        let vy = ps.e0y + ps.ec_ey[ia] + ps.ec_ey[ib];
        let vz = ps.e0z + ps.ec_ez[ia] + ps.ec_ez[ib];
        let mut c = vx * vx + vy * vy + vz * vz;
        c += ps.mt_s[ps.t_a + i];
        c += ps.mt_s[ps.t_b + j];
        c
    }

    /// Allocator.PairTail
    fn pair_tail(&self, ps: &PairScratch, mut c: f32, i: usize, j: usize) -> f32 {
        let ca = ps.ec_c[ps.o_a + i];
        let cb = ps.ec_c[ps.o_b + j];
        if ps.jp >= 0 {
            c += self.pair_reg_at(ps.jp as usize, ca, cb);
        }
        if ps.jp2 >= 0 {
            c += self.pair_reg_at(ps.jp2 as usize, cb, ca);
        }
        if self.io.fight_k > 0.0 {
            let mut g1 = ps.base_g[1] + ps.mt_t1[ps.t_a + i] + ps.mt_t1[ps.t_b + j];
            let mut g2 = ps.base_g[2] + ps.mt_t2[ps.t_a + i] + ps.mt_t2[ps.t_b + j];
            let mut g3 = ps.base_g[3] + ps.mt_t3[ps.t_a + i] + ps.mt_t3[ps.t_b + j];
            if ps.js >= 0 {
                let js = ps.js as usize;
                let v = if js == ps.b_a {
                    self.split_term(js, ca, ps.ec_mx[ps.o_a + i], cb)
                } else {
                    self.split_term(js, cb, ps.ec_mx[ps.o_b + j], ca)
                };
                let gj = self.grp[js];
                if gj == 1 {
                    g1 += v;
                } else if gj == 2 {
                    g2 += v;
                } else {
                    g3 += v;
                }
            }
            let sg = if self.hint != 0.0 { self.hint } else if g1 + g2 + g3 >= 0.0 { 1.0 } else { -1.0 };
            let fw = &self.io.fight_w;
            let mut t = 0.0f64;
            if g1 * sg < 0.0 {
                t += fw[1] as f64 * g1 * g1;
            }
            if g2 * sg < 0.0 {
                t += fw[2] as f64 * g2 * g2;
            }
            if g3 * sg < 0.0 {
                t += fw[3] as f64 * g3 * g3;
            }
            c += (self.io.fight_k as f64 * t) as f32;
        }
        if self.io.pitch_fight_k > 0.0 {
            let mut pa = ps.base_p1;
            let mut pb = ps.base_p2;
            let ga = self.pgrp[ps.b_a];
            let gb = self.pgrp[ps.b_b];
            if ga == 1 {
                pa += ps.mt_d[ps.t_a + i];
            } else if ga == 2 {
                pb += ps.mt_d[ps.t_a + i];
            }
            if gb == 1 {
                pa += ps.mt_d[ps.t_b + j];
            } else if gb == 2 {
                pb += ps.mt_d[ps.t_b + j];
            }
            c += (self.io.pitch_fight_k as f64 * self.pf_term(pa, pb)) as f32;
        }
        c
    }

    #[inline(always)]
    fn pair_cost_full(&self, ps: &PairScratch, i: usize, j: usize) -> f32 {
        let h = self.pair_head(ps, i, j);
        self.pair_tail(ps, h, i, j)
    }

    /// Allocator.PairBlockSearch
    fn pair_block_search(&mut self, ps: &mut PairScratch) {
        let count = self.cnt;
        for i in 0..count {
            ps.y0a[i] = self.ma(i, 0.0).y as f64;
        }
        for i in 0..count {
            self.pair_cache_one(ps, i);
            ps.ec_n[i] = -1;
        }
        for _pass in 0..self.io.pair_passes.max(0) {
            let mut moved = false;
            for k in 0..self.n_blk {
                let a = self.blk_a[k];
                let b = self.blk_b[k];
                if self.search_block(ps, a, b, self.blk_k[k], count) {
                    moved = true;
                    for i in 0..count {
                        let pi = self.pidx[i];
                        if i == a || i == b || (self.grp[i] != 0 && self.stab_split_mode(i) && (pi == a as i32 || pi == b as i32)) {
                            self.pair_cache_one(ps, i);
                        }
                        if i == a || i == b || pi == a as i32 || pi == b as i32 || self.pidx[a] == i as i32 || self.pidx[b] == i as i32 {
                            ps.mt_ok[2 * i] = false;
                            ps.mt_ok[2 * i + 1] = false;
                        }
                    }
                    ps.ec_n[a] = -1;
                    ps.ec_n[b] = -1;
                }
            }
            if !moved {
                break;
            }
        }
    }

    /// Allocator.SearchBlock
    fn search_block(&mut self, ps: &mut PairScratch, a: usize, b: usize, kind: usize, count: usize) -> bool {
        if ps.ec_n[a] < 0 {
            self.ec_build(ps, a);
        }
        if ps.ec_n[b] < 0 {
            self.ec_build(ps, b);
        }
        let na = ps.ec_n[a] as usize;
        let nb = ps.ec_n[b] as usize;
        if na < 2 && nb < 2 {
            return false;
        }
        let mode_a: i32 = if self.pidx[a] == b as i32 || self.pidx[b] == a as i32 { 1 } else { 0 };
        if mode_a == 1 && (self.pair_relations(a) != 1 || self.pair_relations(b) != 1) {
            return false;
        }
        if !ps.mt_ok[2 * a + mode_a as usize] {
            self.mt_build(ps, a, mode_a);
        }
        if !ps.mt_ok[2 * b + mode_a as usize] {
            self.mt_build(ps, b, mode_a);
        }
        ps.b_a = a;
        ps.b_b = b;
        ps.o_a = a * MAX_CAND;
        ps.o_b = b * MAX_CAND;
        ps.t_a = (2 * a + mode_a as usize) * MAX_CAND;
        ps.t_b = (2 * b + mode_a as usize) * MAX_CAND;
        ps.jp = if self.pidx[a] == b as i32 { a as i32 } else { -1 };
        ps.jp2 = if self.pidx[b] == a as i32 { b as i32 } else { -1 };
        ps.js = -1;
        if self.grp[a] != 0 && self.stab_split_mode(a) && self.pidx[a] == b as i32 {
            ps.js = a as i32;
        } else if self.grp[b] != 0 && self.stab_split_mode(b) && self.pidx[b] == a as i32 {
            ps.js = b as i32;
        }
        let mut oth = V3::ZERO;
        for g in 0..=FIGHT_GROUPS {
            ps.base_g[g] = 0.0;
        }
        ps.base_p1 = 0.0;
        ps.base_p2 = 0.0;
        for i in 0..count {
            if i == a || i == b {
                continue;
            }
            oth = oth + ps.mv_all[i];
            let pi = self.pidx[i];
            if self.grp[i] != 0 && !(self.stab_split_mode(i) && (pi == a as i32 || pi == b as i32)) {
                ps.base_g[self.grp[i] as usize] += ps.rt_all[i];
            }
            if self.pgrp[i] == 1 {
                ps.base_p1 += ps.py_all[i];
            } else if self.pgrp[i] == 2 {
                ps.base_p2 += ps.py_all[i];
            }
        }
        let e0 = self.E(oth - self.io.target);
        ps.e0x = e0.x;
        ps.e0y = e0.y;
        ps.e0z = e0.z;
        let mut best = self.pair_cost_full(ps, 0, 0);
        let mut bound = best - 1E-09f32;
        let mut bi = 0usize;
        let mut bj = 0usize;
        {
            let ob = ps.o_b;
            let tb = ps.t_b;
            for i in 0..na {
                let ux = ps.e0x + ps.ec_ex[ps.o_a + i];
                let uy = ps.e0y + ps.ec_ey[ps.o_a + i];
                let uz = ps.e0z + ps.ec_ez[ps.o_a + i];
                let sa = ps.mt_s[ps.t_a + i];
                let bx = &ps.ec_ex[ob..ob + nb];
                let by = &ps.ec_ey[ob..ob + nb];
                let bz = &ps.ec_ez[ob..ob + nb];
                let bs = &ps.mt_s[tb..tb + nb];
                let j0 = if i == 0 { 1 } else { 0 };
                for j in j0..nb {
                    let vx = ux + bx[j];
                    let vy = uy + by[j];
                    let vz = uz + bz[j];
                    let mut c = vx * vx + vy * vy + vz * vz;
                    c += sa;
                    c += bs[j];
                    if c < bound {
                        c = self.pair_tail(ps, c, i, j);
                        if c < bound {
                            best = c;
                            bound = best - 1E-09f32;
                            bi = i;
                            bj = j;
                        }
                    }
                }
            }
        }
        if bi == 0 && bj == 0 {
            return false;
        }
        let ea = *self.ae(a);
        let eb = *self.ae(b);
        let g0 = ps.ec_gi[ps.o_a + bi];
        if g0 > 0 && g0 < ea.n - 1 {
            self.trial(ps, a, mode_a, ea.grid[ix(g0 - 1)]);
            let j_l = self.pair_cost_full(ps, TRIAL, bj);
            self.trial(ps, a, mode_a, ea.grid[ix(g0 + 1)]);
            let j_r = self.pair_cost_full(ps, TRIAL, bj);
            let den = j_l - 2.0f32 * best + j_r;
            if den > 1E-12f32 {
                let c_n = clampf(ea.grid[ix(g0)] + clampf(0.5f32 * (j_l - j_r) / den, -0.5f32, 0.5f32) * ea.step, ea.lo, ea.hi);
                self.trial(ps, a, mode_a, c_n);
                let j_n = self.pair_cost_full(ps, TRIAL, bj);
                if j_n < best {
                    best = j_n;
                    bi = TRIAL;
                }
            }
        }
        let g1 = ps.ec_gi[ps.o_b + bj];
        if g1 > 0 && g1 < eb.n - 1 {
            self.trial(ps, b, mode_a, eb.grid[ix(g1 - 1)]);
            let j_l = self.pair_cost_full(ps, bi, TRIAL);
            self.trial(ps, b, mode_a, eb.grid[ix(g1 + 1)]);
            let j_r = self.pair_cost_full(ps, bi, TRIAL);
            let den = j_l - 2.0f32 * best + j_r;
            if den > 1E-12f32 {
                let c_n = clampf(eb.grid[ix(g1)] + clampf(0.5f32 * (j_l - j_r) / den, -0.5f32, 0.5f32) * eb.step, eb.lo, eb.hi);
                self.trial(ps, b, mode_a, c_n);
                let j_n = self.pair_cost_full(ps, bi, TRIAL);
                if j_n < best {
                    #[allow(unused_assignments)]
                    {
                        best = j_n;
                    }
                    bj = TRIAL;
                }
            }
        }
        self.x[a] = ps.ec_c[ps.o_a + bi] as f64;
        self.x[b] = ps.ec_c[ps.o_b + bj] as f64;
        self.io.pair_moves += 1;
        self.io.pair_moves_k[kind] += 1;
        true
    }

    /// Allocator.PairedSearch
    fn paired_search(&mut self, total: &mut V3) {
        let count = self.cnt;
        let target = self.io.target;
        let mut it = usize::MAX;
        for i in 0..count {
            if self.ae(i).kind == 1 {
                it = i;
                break;
            }
        }
        if it == usize::MAX {
            return;
        }
        let tv = *self.ae(it);
        if tv.hi - tv.lo < 0.05f32 {
            return;
        }
        let t_lo = m_at(&tv, tv.lo);
        let t_hi = m_at(&tv, tv.hi);
        let k_y = (t_hi.y - t_lo.y) / (tv.hi - tv.lo);
        if absf(k_y) < 1E-06f32 {
            return;
        }
        let pair_horizon = self.io.pair_horizon;
        for s in 0..count {
            let se = *self.ae(s);
            if se.kind != 2 {
                continue;
            }
            let xs = self.x[s] as f32;
            let xt = self.x[it] as f32;
            let m_s = m_at(&se, xs);
            let m_t = m_at(&tv, xt);
            let others = *total - m_s - m_t;
            let ps = self.prev[s] as f32;
            let pt = self.prev[it] as f32;
            let mut best = {
                let a = self.cost(*total - target);
                let b = self.reg(&se, xs, ps);
                let c = self.pair_reg_with(s, xs);
                let d = self.reg(&tv, xt, pt);
                let e = self.fight_cost_with(s, xs);
                let f = self.pf_pair(s, xs, it, xt);
                a + b + c + d + e + f
            };
            let mut bs = xs;
            let mut bt = xt;
            let mut b_ms = m_s;
            let mut b_mt = m_t;
            for g in 0..se.n {
                let gs = se.grid[ix(g)];
                if gs < se.lo - 0.0001f32
                    || gs > se.hi + 0.0001f32
                    || absf(gs - se.now) > maxf(se.step, pair_horizon * maxf(1.0f32, se.rate))
                {
                    continue;
                }
                let ms = se.m[ix(g)];
                let gt = clampf(xt + (m_s.y - ms.y) / k_y, tv.lo, tv.hi);
                let mt = m_at(&tv, gt);
                let c = {
                    let a = self.cost(others + ms + mt - target);
                    let b = self.reg(&se, gs, ps);
                    let c = self.pair_reg_with(s, gs);
                    let d = self.reg(&tv, gt, pt);
                    let e = self.fight_cost_with(s, gs);
                    let f = self.pf_pair(s, gs, it, gt);
                    a + b + c + d + e + f
                };
                if c < best - 1E-09f32 {
                    best = c;
                    bs = gs;
                    bt = gt;
                    b_ms = ms;
                    b_mt = mt;
                }
            }
            if bs != xs {
                self.x[s] = bs as f64;
                self.x[it] = bt as f64;
                *total = others + b_ms + b_mt;
                self.io.paired_moves += 1;
            }
        }
    }

    /// Allocator.ReachFrom
    fn reach_from(&mut self) {
        let n_all = self.e.len();
        self.rch_n = 0;
        for i in 0..n_all {
            let e = &self.e[i];
            if e.active == 0 || e.reach_tau <= 0.0 {
                continue;
            }
            let reach = maxf(1.0f32, e.rate) * e.reach_tau;
            if absf(e.cmd - e.now) <= reach {
                continue;
            }
            let k = self.rch_n;
            self.rch_e[k] = i;
            self.rch_dest[k] = e.cmd;
            self.rch_lo[k] = e.lo;
            self.rch_hi[k] = e.hi;
            self.rch_n += 1;
        }
        if self.rch_n == 0 {
            return;
        }
        self.io.reach_moves += 1;
        for i in 0..n_all {
            self.e[i].cmd = self.rch_cmd0[i];
        }
        for i in 0..self.rch_n {
            let dest = self.rch_dest[i];
            let e = &mut self.e[self.rch_e[i]];
            let reach = maxf(1.0f32, e.rate) * e.reach_tau;
            let at = e.now + clampf(dest - e.now, -reach, reach);
            e.cmd = at;
            e.hi = at;
            e.lo = at;
        }
        self.solve_core();
        for i in 0..self.rch_n {
            let e = &mut self.e[self.rch_e[i]];
            e.lo = self.rch_lo[i];
            e.hi = self.rch_hi[i];
            e.cmd = self.rch_dest[i];
        }
    }

    #[inline(always)]
    fn in_rch(&self, i: usize) -> bool {
        for k in 0..self.rch_n {
            if self.rch_e[k] == i {
                return true;
            }
        }
        false
    }

    /// Allocator.LinBuild: the answering effectors (active, a table, not `slow`) around their commands
    fn lin_build(&mut self, slow: &[bool; MAX_EFF]) -> bool {
        let n = self.e.len();
        self.ln_n = 0;
        let (mut sxx, mut sxy, mut sxz, mut syy, mut syz, mut szz) = (0.0f32, 0.0f32, 0.0f32, 0.0f32, 0.0f32, 0.0f32);
        for k in 0..n {
            let e = &self.e[k];
            if e.active == 0 || e.n < 2 || slow[k] {
                continue;
            }
            let lo = maxf(e.min, e.lo);
            let hi = minf(e.max, e.hi);
            if hi - lo < 0.01f32 {
                continue;
            }
            let c = clampf(e.cmd, lo, hi);
            let h = maxf(0.5f32, 0.5f32 * e.step);
            let c0 = maxf(lo, c - h);
            let c1 = minf(hi, c + h);
            if c1 - c0 < 1e-3f32 {
                continue;
            }
            let g = self.E(m_at(e, c1) - m_at(e, c0)).div(c1 - c0);
            let q = self.ln_n;
            self.ln_g[q] = g;
            self.ln_c[q] = c;
            self.ln_lo[q] = lo;
            self.ln_hi[q] = hi;
            sxx += g.x * g.x;
            sxy += g.x * g.y;
            sxz += g.x * g.z;
            syy += g.y * g.y;
            syz += g.y * g.z;
            szz += g.z * g.z;
            self.ln_n += 1;
        }
        let eps = 1e-4f32 * (sxx + syy + szz) / 3f32 + 1e-9f32;
        let a = sxx + eps;
        let b = sxy;
        let c3 = sxz;
        let d = sxy;
        let ee = syy + eps;
        let f = syz;
        let g3 = sxz;
        let hh = syz;
        let ii = szz + eps;
        let aa = ee * ii - f * hh;
        let bb = -(d * ii - f * g3);
        let cc = d * hh - ee * g3;
        let det = a * aa + b * bb + c3 * cc;
        if !(absf(det) > 1e-20f32) {
            return false;
        }
        let id = 1.0f32 / det;
        self.ln_m = [
            aa * id,
            -(b * ii - c3 * hh) * id,
            (b * f - c3 * ee) * id,
            bb * id,
            (a * ii - c3 * g3) * id,
            -(a * f - c3 * d) * id,
            cc * id,
            -(a * hh - b * g3) * id,
            (a * ee - b * d) * id,
        ];
        true
    }

    /// Allocator.LinScore
    fn lin_score(&self, r: V3) -> f32 {
        let m = &self.ln_m;
        let v = V3 {
            x: m[0] * r.x + m[1] * r.y + m[2] * r.z,
            y: m[3] * r.x + m[4] * r.y + m[5] * r.z,
            z: m[6] * r.x + m[7] * r.y + m[8] * r.z,
        };
        let mut got = V3::ZERO;
        for q in 0..self.ln_n {
            let g = self.ln_g[q];
            let du = g.x * v.x + g.y * v.y + g.z * v.z;
            let c = self.ln_c[q];
            let u = clampf(c + du, self.ln_lo[q], self.ln_hi[q]);
            got = got + g.mul(u - c);
        }
        (r - got).sqr()
    }

    /// Allocator.HorizonCost
    fn horizon_cost(&self, slow: &[bool; MAX_EFF], j_dest: f32) -> f32 {
        let n = self.e.len();
        let kk = if self.io.horizon_steps > 1 { self.io.horizon_steps } else { 1 };
        let h = self.io.horizon_t / kk as f32;
        let mut t_arr = 0.0f32;
        for i in 0..n {
            if !slow[i] {
                continue;
            }
            let e = &self.e[i];
            t_arr = maxf(t_arr, absf(e.cmd - e.now) / maxf(1.0f32, e.rate));
        }
        if t_arr <= h {
            return j_dest;
        }
        let target = self.io.target;
        let mut oth = V3::ZERO;
        for i in 0..n {
            let e = &self.e[i];
            if e.active != 0 && !slow[i] {
                oth = oth + m_at(e, e.cmd);
            }
        }
        let mut dest = oth;
        for i in 0..n {
            if slow[i] {
                let e = &self.e[i];
                dest = dest + m_at(e, e.cmd);
            }
        }
        let s0 = self.lin_score(self.E(target - dest));
        let mut acc = 0.0f32;
        for k in 1..=kk {
            let t = k as f32 * h;
            if t >= t_arr {
                acc += j_dest * (kk - k + 1) as f32;
                break;
            }
            let f = t / t_arr;
            let mut tot = oth;
            for i in 0..n {
                if slow[i] {
                    let e = &self.e[i];
                    tot = tot + m_at(e, e.now + f * (e.cmd - e.now));
                }
            }
            acc += j_dest + maxf(0.0f32, self.lin_score(self.E(target - tot)) - s0);
        }
        acc / kk as f32
    }

    /// Allocator.CrossesFold (5.1.1)
    fn crosses_fold(&self, ie: usize, a: f32, b: f32) -> bool {
        let e = &self.e[ie];
        let n = e.n as usize;
        if n < 3 {
            return false;
        }
        let lo = minf(a, b);
        let hi = maxf(a, b);
        let ma = self.E(m_at(e, lo));
        let mb = self.E(m_at(e, hi));
        let ch = mb - ma;
        let mut prev_p = 0.0f32;
        for i in 0..n {
            let g = e.grid[i];
            if g <= lo || g >= hi {
                continue;
            }
            let d = self.E(e.m[i]) - ma;
            let p = d.x * ch.x + d.y * ch.y + d.z * ch.z;
            if p < prev_p {
                return true;
            }
            prev_p = p;
        }
        let d_end = mb - ma;
        d_end.x * ch.x + d_end.y * ch.y + d_end.z * ch.z < prev_p
    }

    /// Allocator.HorizonProxy
    fn horizon_proxy(&self, il: usize, ir: usize, cl: f32, cr: f32, r0: V3, s_dest: f32) -> f32 {
        let kk = if self.io.horizon_steps > 1 { self.io.horizon_steps } else { 1 };
        let h = self.io.horizon_t / kk as f32;
        let sl = &self.e[il];
        let sr = &self.e[ir];
        let t_arr = maxf(absf(cl - sl.now) / maxf(1.0f32, sl.rate), absf(cr - sr.now) / maxf(1.0f32, sr.rate));
        if t_arr <= h {
            return s_dest;
        }
        let mut acc = 0.0f32;
        for k in 1..=kk {
            let t = k as f32 * h;
            if t >= t_arr {
                acc += s_dest * (kk - k + 1) as f32;
                break;
            }
            let f = t / t_arr;
            let pl = sl.now + f * (cl - sl.now);
            let pr = sr.now + f * (cr - sr.now);
            acc += s_dest + maxf(0.0f32, self.lin_score(r0 - self.E(m_at(sl, pl)) - self.E(m_at(sr, pr))) - s_dest);
        }
        acc / kk as f32
    }

    /// Allocator.HorizonPick, its sequential path
    fn horizon_pick(&mut self) {
        let n_all = self.e.len();
        let target = self.io.target;
        let j_d = self.io.last_cost;
        let mut h_d = [0.0f32; MAX_EFF];
        for i in 0..n_all {
            h_d[i] = self.e[i].cmd;
        }
        let mut slow = [false; MAX_EFF];
        for i in 0..n_all {
            let e = &self.e[i];
            slow[i] = e.active != 0 && e.reach_tau > 0.0 && e.two_horizon != 0;
        }
        self.reach_from();
        let mut need_b = self.rch_n > 0;
        if need_b {
            let mut any_th = false;
            for k in 0..self.rch_n {
                if self.e[self.rch_e[k]].two_horizon != 0 {
                    any_th = true;
                }
            }
            need_b = any_th;
        }
        let mut stab = [false; MAX_EFF];
        let mut n_stab = 0usize;
        let mut st_i = [0usize; 2];
        for i in 0..n_all {
            if slow[i] && self.e[i].kind == 2 && self.e[i].n >= 2 {
                stab[i] = true;
                if n_stab < 2 {
                    st_i[n_stab] = i;
                }
                n_stab += 1;
            }
        }
        let scan = self.io.branch_scan != 0 && n_stab == 2;
        self.io.last_needed_pick = if need_b { 1 } else { 0 };
        if !need_b && !scan {
            return;
        }
        let mut h_e = [0.0f32; MAX_EFF];
        for i in 0..n_all {
            h_e[i] = self.e[i].cmd;
        }
        let j_e = self.io.last_cost;
        let res_e = self.io.res_dbg;
        for i in 0..n_all {
            self.e[i].cmd = h_d[i];
        }
        let jh_d = if self.lin_build(&slow) { self.horizon_cost(&slow, j_d) } else { j_d };
        let mut best = jh_d;
        let mut pick = 0;
        let mut j_b = 0.0f32;
        let mut res_b = V3::ZERO;
        let mut h_b = [0.0f32; MAX_EFF];
        if need_b {
            let mut th_lo = [0.0f32; MAX_EFF];
            let mut th_hi = [0.0f32; MAX_EFF];
            for i in 0..n_all {
                let cmd0 = self.rch_cmd0[i];
                let e = &mut self.e[i];
                th_lo[i] = e.lo;
                th_hi[i] = e.hi;
                e.cmd = cmd0;
                if e.active != 0 && e.reach_tau > 0.0 && e.two_horizon != 0 {
                    let reach = maxf(1.0f32, e.rate) * e.reach_tau;
                    let lo = maxf(e.lo, minf(e.now, e.hi) - reach);
                    let hi = minf(e.hi, maxf(e.now, e.lo) + reach);
                    if lo <= hi {
                        e.lo = lo;
                        e.hi = hi;
                    }
                }
            }
            self.solve_core();
            j_b = self.io.last_cost;
            res_b = self.io.res_dbg;
            for i in 0..n_all {
                self.e[i].lo = th_lo[i];
                self.e[i].hi = th_hi[i];
            }
            for i in 0..n_all {
                h_b[i] = self.e[i].cmd;
            }
            for i in 0..n_all {
                self.e[i].cmd = h_b[i];
            }
            let jh_b = if self.lin_build(&slow) { self.horizon_cost(&slow, j_b) } else { j_b };
            // 5.1.1: destination vs reach plan by the 2.42 rule (same basin)
            if 2.0f32 * j_b <= j_e + j_d {
                best = jh_b;
                pick = 1;
            }
        }
        let mut j_ce = 0.0f32;
        let mut res_ce = V3::ZERO;
        let mut h_ce = [0.0f32; MAX_EFF];
        if scan {
            for i in 0..n_all {
                self.e[i].cmd = h_d[i];
            }
            let (il, ir) = (st_i[0], st_i[1]);
            if self.lin_build(&stab) {
                let mut oth = V3::ZERO;
                for i in 0..n_all {
                    let e = &self.e[i];
                    if e.active != 0 && i != il && i != ir {
                        oth = oth + m_at(e, e.cmd);
                    }
                }
                let r0 = self.E(target - oth);
                let (l_lo, l_hi, r_lo, r_hi) = {
                    let sl = &self.e[il];
                    let sr = &self.e[ir];
                    (maxf(sl.min, sl.lo) - 1e-4f32, minf(sl.max, sl.hi) + 1e-4f32, maxf(sr.min, sr.lo) - 1e-4f32, minf(sr.max, sr.hi) + 1e-4f32)
                };
                let st = if self.io.branch_stride > 1 { self.io.branch_stride as usize } else { 1 };
                let mut s_best = f32::MAX;
                let mut i_b: i32 = -1;
                let mut j_bi: i32 = -1;
                let nl = self.e[il].n as usize;
                let nr = self.e[ir].n as usize;
                let mut i = 0usize;
                while i < nl {
                    let gl = self.e[il].grid[i];
                    if !(gl < l_lo || gl > l_hi) {
                        let r_l = r0 - self.E(self.e[il].m[i]);
                        let mut j = 0usize;
                        while j < nr {
                            let gr = self.e[ir].grid[j];
                            if !(gr < r_lo || gr > r_hi) {
                                let sc = self.lin_score(r_l - self.E(self.e[ir].m[j]));
                                if sc < s_best {
                                    s_best = sc;
                                    i_b = i as i32;
                                    j_bi = j as i32;
                                }
                            }
                            j += st;
                        }
                    }
                    i += st;
                }
                let d_l = self.e[il].cmd;
                let d_r = self.e[ir].cmd;
                if i_b >= 0 {
                    let gbl = self.e[il].grid[i_b as usize];
                    let gbr = self.e[ir].grid[j_bi as usize];
                    // 5.1.1: only a pair across a fold is another branch
                    if self.crosses_fold(il, d_l, gbl) || self.crosses_fold(ir, d_r, gbr) {
                        let s_d = self.lin_score(r0 - self.E(m_at(&self.e[il], d_l)) - self.E(m_at(&self.e[ir], d_r)));
                        let p_d = self.horizon_proxy(il, ir, d_l, d_r, r0, s_d);
                        let p_c = self.horizon_proxy(il, ir, gbl, gbr, r0, s_best);
                        if p_c < p_d {
                            self.io.branch_refines += 1;
                            let (sllo, slhi, srlo, srhi) = (self.e[il].lo, self.e[il].hi, self.e[ir].lo, self.e[ir].hi);
                            self.e[il].lo = gbl;
                            self.e[il].hi = gbl;
                            self.e[il].cmd = gbl;
                            self.e[ir].lo = gbr;
                            self.e[ir].hi = gbr;
                            self.e[ir].cmd = gbr;
                            self.solve_core();
                            self.e[il].lo = sllo;
                            self.e[il].hi = slhi;
                            self.e[ir].lo = srlo;
                            self.e[ir].hi = srhi;
                            self.solve_core();
                            let j_c = self.io.last_cost;
                            let jh_c = if self.lin_build(&slow) { self.horizon_cost(&slow, j_c) } else { j_c };
                            let (el, er) = (self.e[il].cmd, self.e[ir].cmd);
                            if jh_c < best && (self.crosses_fold(il, d_l, el) || self.crosses_fold(ir, d_r, er)) {
                                best = jh_c;
                                pick = 2;
                                self.reach_from();
                                j_ce = self.io.last_cost;
                                res_ce = self.io.res_dbg;
                                for k in 0..n_all {
                                    h_ce[k] = self.e[k].cmd;
                                }
                            }
                        }
                    }
                }
            }
        }
        let _ = best;
        self.io.horizon_picks += 1;
        if pick == 1 {
            self.io.two_horizon_bound += 1;
            for i in 0..n_all {
                self.e[i].cmd = h_b[i];
            }
            self.io.last_cost = j_b;
            self.io.res_dbg = res_b;
            return;
        }
        if pick == 2 {
            self.io.branch_taken += 1;
            for i in 0..n_all {
                self.e[i].cmd = h_ce[i];
            }
            self.io.last_cost = j_ce;
            self.io.res_dbg = res_ce;
            return;
        }
        for i in 0..n_all {
            self.e[i].cmd = h_e[i];
        }
        self.io.last_cost = j_e;
        self.io.res_dbg = res_e;
    }

    /// Allocator.SolveInner, its sequential path (the plugin's worker-thread reach plan gives the same result by construction)
    fn solve_inner(&mut self) {
        let n_all = self.e.len();
        let mut any = false;
        if self.io.reach_pass != 0 {
            for e in self.e.iter() {
                if e.active != 0 && e.reach_tau > 0.0 {
                    any = true;
                    break;
                }
            }
        }
        if !any {
            self.solve_core();
            return;
        }
        for i in 0..n_all {
            self.rch_cmd0[i] = self.e[i].cmd;
        }
        self.solve_core();
        if self.io.horizon_on != 0 && self.io.two_horizon != 0 {
            self.horizon_pick();
            return;
        }
        if self.io.two_horizon == 0 {
            self.reach_from();
            return;
        }
        let j_d = self.io.last_cost;
        self.reach_from();
        if self.rch_n == 0 {
            self.io.last_needed_pick = 0;
            return;
        }
        let mut any_th = false;
        for k in 0..self.rch_n {
            if self.e[self.rch_e[k]].two_horizon != 0 {
                any_th = true;
            }
        }
        if !any_th {
            self.io.last_needed_pick = 0;
            return;
        }
        self.io.last_needed_pick = 1;
        let mut j_r = self.io.last_cost;
        let mut th_r = [0.0f32; MAX_EFF];
        for i in 0..n_all {
            th_r[i] = self.e[i].cmd;
        }
        let target = self.io.target;
        if self.io.two_horizon_path != 0 {
            let mut oth = V3::ZERO;
            for i in 0..n_all {
                let e = &self.e[i];
                if e.active != 0 && !self.in_rch(i) {
                    oth = oth + m_at(e, e.cmd);
                }
            }
            let mut acc = 0.0f32;
            for k in 1..=8i32 {
                let f = k as f32 / 8.0f32;
                let mut tot = oth;
                for i in 0..self.rch_n {
                    let e = &self.e[self.rch_e[i]];
                    tot = tot + m_at(e, e.now + f * (e.cmd - e.now));
                }
                acc += self.cost(tot - target);
            }
            j_r = maxf(j_r, acc / 8.0f32);
        }
        let mut th_lo = [0.0f32; MAX_EFF];
        let mut th_hi = [0.0f32; MAX_EFF];
        for i in 0..n_all {
            let cmd0 = self.rch_cmd0[i];
            let e = &mut self.e[i];
            th_lo[i] = e.lo;
            th_hi[i] = e.hi;
            e.cmd = cmd0;
            if e.active != 0 && e.reach_tau > 0.0 && e.two_horizon != 0 {
                let reach = maxf(1.0f32, e.rate) * e.reach_tau;
                let lo = maxf(e.lo, minf(e.now, e.hi) - reach);
                let hi = minf(e.hi, maxf(e.now, e.lo) + reach);
                if lo <= hi {
                    e.lo = lo;
                    e.hi = hi;
                }
            }
        }
        self.solve_core();
        let j_b = self.io.last_cost;
        for i in 0..n_all {
            self.e[i].lo = th_lo[i];
            self.e[i].hi = th_hi[i];
        }
        if 2.0f32 * j_b <= j_r + j_d {
            self.io.two_horizon_bound += 1;
            return;
        }
        for i in 0..n_all {
            self.e[i].cmd = th_r[i];
        }
    }
}

/// Allocator.SolveLinear (Gaussian elimination with partial pivoting on copies; false when singular)
fn solve_linear(a0: &[[f64; MAX_EFF]; MAX_EFF], b0: &[f64; MAX_EFF], out_x: &mut [f64; MAX_EFF], n: usize) -> bool {
    let mut array = *a0;
    let mut array2 = *b0;
    for i in 0..n {
        let mut num = i;
        let mut num2 = array[i][i].abs();
        for j in (i + 1)..n {
            if array[j][i].abs() > num2 {
                num2 = array[j][i].abs();
                num = j;
            }
        }
        if num2 < 1E-18f64 {
            return false;
        }
        if num != i {
            for k in 0..n {
                let num3 = array[i][k];
                array[i][k] = array[num][k];
                array[num][k] = num3;
            }
            let num4 = array2[i];
            array2[i] = array2[num];
            array2[num] = num4;
        }
        for l in (i + 1)..n {
            let num5 = array[l][i] / array[i][i];
            if num5 != 0.0 {
                for m in i..n {
                    array[l][m] -= num5 * array[i][m];
                }
                array2[l] -= num5 * array2[i];
            }
        }
    }
    for num6 in (0..n).rev() {
        let mut num7 = array2[num6];
        for num8 in (num6 + 1)..n {
            num7 -= array[num6][num8] * out_x[num8];
        }
        out_x[num6] = num7 / array[num6][num6];
    }
    true
}

// ---------------------------------------------------------------------------------------------------------------------
// 4.1.0: the grid kernels
// ---------------------------------------------------------------------------------------------------------------------

/// UnityEngine.Quaternion (x, y, z, w).
#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Q4 {
    pub x: f32,
    pub y: f32,
    pub z: f32,
    pub w: f32,
}

impl Q4 {
    /// UnityEngine.Quaternion: operator *(Quaternion, Vector3), expression for expression
    #[inline(always)]
    fn rot(self, p: V3) -> V3 {
        let num = self.x * 2.0f32;
        let num2 = self.y * 2.0f32;
        let num3 = self.z * 2.0f32;
        let num4 = self.x * num;
        let num5 = self.y * num2;
        let num6 = self.z * num3;
        let num7 = self.x * num2;
        let num8 = self.x * num3;
        let num9 = self.y * num3;
        let num10 = self.w * num;
        let num11 = self.w * num2;
        let num12 = self.w * num3;
        V3 {
            x: (1.0f32 - (num5 + num6)) * p.x + (num7 - num12) * p.y + (num8 + num11) * p.z,
            y: (num7 + num12) * p.x + (1.0f32 - (num4 + num6)) * p.y + (num9 - num10) * p.z,
            z: (num8 - num11) * p.x + (num9 + num10) * p.y + (1.0f32 - (num4 + num5)) * p.z,
        }
    }
}

/// UnityEngine.Vector3.Cross
#[inline(always)]
fn v3_cross(a: V3, b: V3) -> V3 {
    V3 { x: a.y * b.z - a.z * b.y, y: a.z * b.x - a.x * b.z, z: a.x * b.y - a.y * b.x }
}

/// UnityEngine.Vector3.magnitude: (float)Math.Sqrt(x * x + y * y + z * z)
#[inline(always)]
fn v3_mag(a: V3) -> f32 {
    ((a.x * a.x + a.y * a.y + a.z * a.z) as f64).sqrt() as f32
}

/// UnityEngine.Vector3: operator -(Vector3)
#[inline(always)]
fn v3_neg(a: V3) -> V3 {
    V3 { x: -a.x, y: -a.y, z: -a.z }
}

/// UnityEngine.Mathf.Atan2: (float)Math.Atan2(y, x)
#[inline(always)]
fn atan2f(y: f32, x: f32) -> f32 {
    (y as f64).atan2(x as f64) as f32
}

/// The airfoil tables' length (AirfoilTable: 128 points, index = angle * 20.37185 + 64).
pub const FOIL_N: usize = 128;

/// AirfoilTable.Read. `Err` for a NaN index: the C# code throws there (an array index from `(int)NaN`), so the plugin
/// has to run its own loop and throw as it always did.
#[inline(always)]
unsafe fn foil_read(t: *const f32, idx: f32) -> Result<f32, ()> {
    if idx <= 0.0f32 {
        return Ok(*t);
    }
    if idx >= 127.0f32 {
        return Ok(*t.add(127));
    }
    if idx.is_nan() {
        return Err(());
    }
    let num = idx as i32;
    let num2 = idx - (num as f32);
    let a = *t.add(num as usize);
    let b = *t.add(num as usize + 1);
    Ok(a + (b - a) * num2)
}

/// AirfoilTable.Get, the tabulated branch (the plugin keeps a `generic` airfoil in C#: that branch is sin / cos).
#[inline(always)]
unsafe fn foil_get(cl: *const f32, cd: *const f32, a: f32) -> Result<(f32, f32), ()> {
    let idx = a * 20.37185f32 + 64.0f32;
    Ok((foil_read(cl, idx)?, foil_read(cd, idx)?))
}

/// One surface, one airflow, every deflection of its grid (240 bytes). The plugin fills it per call; the arrays are its
/// own (pinned for the call).
#[repr(C)]
pub struct GridIn {
    /// per grid point (SurfaceEffector.GridGeom): Quaternion.Inverse(q), q * Vector3.right, com + q * col - cg
    pub g_inv: *const Q4,
    pub g_right: *const V3,
    pub g_arm: *const V3,
    /// per grid point, host section: Quaternion.Inverse(r1), r1 * Vector3.right, Camber.Delta's zero test
    pub gh_inv: *const Q4,
    pub gh_right: *const V3,
    pub gh_zero: *const u8,
    /// the surface's airfoil table and its host's (FOIL_N floats each)
    pub cl: *const f32,
    pub cd: *const f32,
    pub hcl: *const f32,
    pub hcd: *const f32,
    /// mode 0: receives the n moments
    pub out_m: *mut V3,
    pub n: i32,
    /// hostK > 0
    pub host: i32,
    /// hv.sqrMagnitude < 1E-06f
    pub hv_small: i32,
    pub reserved: i32,
    /// the surface's airflow, airflow / speed, 0.5 rho v^2 S eff
    pub v: V3,
    pub vhat: V3,
    pub num: f32,
    /// the host's airflow, its Vector3.normalized, its 0.5 rho v^2 S eff
    pub hv: V3,
    pub hvn: V3,
    pub hq: f32,
    /// hostCom + hostRot * hostCol - cg, and the host's undeflected force (Camber.Foil)
    pub host_arm: V3,
    pub host_base: V3,
    pub root_inv: Q4,
    /// mode 1: IaInv[0,0], IaInv[0,2], IaInv[2,0], IaInv[2,2]
    pub ia: [f32; 4],
    /// mode 1: pitch min, pitch max, roll accel min, max, yaw accel min, max over the grid
    pub out: [f32; 6],
}

/// SurfaceEffector.Eval / EvalFlow for grid point `l`, from the geometry of GridGeom: the flow's part only.
#[inline(always)]
unsafe fn grid_point(g: &GridIn, l: usize) -> Result<V3, ()> {
    let v_flow = g.v;
    let vector = (*g.g_inv.add(l)).rot(v_flow);
    let (c_l, c_d) = foil_get(g.cl, g.cd, atan2f(vector.y, vector.z))?;
    let vector2 = v3_cross(v_flow, *g.g_right.add(l));
    let magnitude = v3_mag(vector2);
    if magnitude < 0.0001f32 {
        return Ok(V3::ZERO);
    }
    let num = g.num;
    // -vector2 / magnitude * (CL * num) - vhat * (CD * num)
    let rhs2 = v3_neg(vector2).div(magnitude).mul(c_l * num) - g.vhat.mul(c_d * num);
    let mut m_f = v3_cross(*g.g_arm.add(l), rhs2);
    if g.host != 0 {
        let mut d_h = V3::ZERO;
        if *g.gh_zero.add(l) == 0 {
            // Camber.FoilPre(hv, Inverse(r1), r1 * right, hostFoil, hq, hv.normalized, small) - hostBase
            let hv = g.hv;
            let vl = (*g.gh_inv.add(l)).rot(hv);
            let (cl, cd) = foil_get(g.hcl, g.hcd, atan2f(vl.y, vl.z))?;
            let n = v3_cross(hv, *g.gh_right.add(l));
            let m = v3_mag(n);
            let f = if m < 1E-06f32 || g.hv_small != 0 {
                V3::ZERO
            } else {
                v3_neg(n).div(m).mul(cl * g.hq) - g.hvn.mul(cd * g.hq)
            };
            d_h = f - g.host_base;
        }
        m_f = m_f + v3_cross(g.host_arm, d_h);
    }
    // Effector.ToAero
    let vector = g.root_inv.rot(m_f);
    Ok(V3 { x: 0.0f32 - vector.z, y: 0.0f32 - vector.x, z: vector.y })
}

/// UnityEngine.Mathf.Min / Max (two floats)
#[inline(always)]
fn mathf_min(a: f32, b: f32) -> f32 {
    if a < b {
        a
    } else {
        b
    }
}

#[inline(always)]
fn mathf_max(a: f32, b: f32) -> f32 {
    if a > b {
        a
    } else {
        b
    }
}

unsafe fn grid_run(g: &mut GridIn, mode: i32) -> Result<(), ()> {
    let n = g.n as usize;
    if mode == 0 {
        for l in 0..n {
            let m = grid_point(g, l)?;
            *g.out_m.add(l) = m;
        }
        return Ok(());
    }
    // the sweep's capacity loop (FcsController.AsymSweepPoint)
    let mut num8 = f32::MAX;
    let mut num9 = f32::MIN;
    let mut num10 = f32::MAX;
    let mut num11 = f32::MIN;
    let mut p_min_s = f32::MAX;
    let mut p_max_s = f32::MIN;
    for l in 0..n {
        let m2 = grid_point(g, l)?;
        p_min_s = mathf_min(p_min_s, m2.y);
        p_max_s = mathf_max(p_max_s, m2.y);
        // AccRoll, AccYaw
        let b = (g.ia[0] * m2.x + g.ia[1] * m2.z) * 57.29578f32;
        let b2 = (g.ia[2] * m2.x + g.ia[3] * m2.z) * 57.29578f32;
        num8 = mathf_min(num8, b);
        num9 = mathf_max(num9, b);
        num10 = mathf_min(num10, b2);
        num11 = mathf_max(num11, b2);
    }
    g.out = [p_min_s, p_max_s, num8, num9, num10, num11];
    Ok(())
}

/// Size of GridIn, for the plugin's layout check.
#[no_mangle]
pub extern "C" fn f22e_fcs_grid_size() -> i32 {
    core::mem::size_of::<GridIn>() as i32
}

/// The grid kernels. mode 0: `out_m[0..n]` = the surface's moment at each grid deflection (the table rebuild);
/// mode 1: `out` = the min / max the asymmetric-load sweep keeps. 0 = done; negative = nothing written may be used and
/// the plugin runs its C# loop: -1 a panic, -2 bad arguments, -7 a NaN airfoil index (the C# code throws there).
///
/// # Safety
/// `g` must point at one valid, writable `GridIn` whose arrays hold `n` elements (`FOIL_N` for the tables).
#[no_mangle]
pub unsafe extern "C" fn f22e_fcs_grid(g: *mut GridIn, mode: i32) -> i32 {
    if g.is_null() {
        return -2;
    }
    let g = &mut *g;
    if g.n < 1 || g.n as usize > 4 * MAX_N || (mode != 0 && mode != 1) {
        return -2;
    }
    if g.g_inv.is_null() || g.g_right.is_null() || g.g_arm.is_null() || g.cl.is_null() || g.cd.is_null() {
        return -2;
    }
    if g.host != 0 && (g.gh_inv.is_null() || g.gh_right.is_null() || g.gh_zero.is_null() || g.hcl.is_null() || g.hcd.is_null()) {
        return -2;
    }
    if mode == 0 && g.out_m.is_null() {
        return -2;
    }
    let r = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| grid_run(g, mode)));
    match r {
        Ok(Ok(())) => 0,
        Ok(Err(())) => -7,
        Err(_) => -1,
    }
}

/// What the plugin checks before it trusts the library.
#[no_mangle]
pub extern "C" fn f22e_fcs_abi() -> i32 {
    ABI
}

/// (size of EffN << 16) | size of AllocIo, for the plugin's layout check.
#[no_mangle]
pub extern "C" fn f22e_fcs_sizes() -> i32 {
    ((core::mem::size_of::<EffN>() as i32) << 16) | (core::mem::size_of::<AllocIo>() as i32)
}

fn validate(io: &AllocIo, effs: &[EffN]) -> i32 {
    let n = effs.len() as i32;
    for e in effs {
        if e.n < 0 || e.n as usize > MAX_N {
            return -3;
        }
        if e.partner < -1 || e.partner >= n {
            return -4;
        }
        if e.roll_group < 0 || e.roll_group > FIGHT_GROUPS as i32 || e.pitch_group < 0 || e.pitch_group > 2 {
            return -5;
        }
    }
    if io.gn_iterations < 0 || io.gn_iterations > 64 {
        return -6;
    }
    0
}

/// Allocator.Solve. `effs` points at `io.n` effectors; their `cmd` fields hold the answer on return (0), `io` the
/// cost, the residual and the counters. A negative return means nothing may be used and the caller solves in C#:
/// -1 a panic (an index the C# code would have thrown on), -2 bad sizes, -3..-6 fields out of range.
///
/// # Safety
/// `io` must point at one valid `AllocIo` and `effs` at `io.n` valid `EffN`, both writable and not aliased for the call.
#[no_mangle]
pub unsafe extern "C" fn f22e_fcs_solve(io: *mut AllocIo, effs: *mut EffN) -> i32 {
    if io.is_null() || effs.is_null() {
        return -2;
    }
    let io = &mut *io;
    if io.n < 0 || io.n as usize > MAX_EFF {
        return -2;
    }
    let effs = core::slice::from_raw_parts_mut(effs, io.n as usize);
    let v = validate(io, effs);
    if v != 0 {
        return v;
    }
    io.reach_moves = 0;
    io.two_horizon_bound = 0;
    io.coord_newton_moves = 0;
    io.paired_moves = 0;
    io.core_solves = 0;
    io.pair_moves = 0;
    io.pair_moves_k = [0; PAIR_KINDS];
    io.horizon_picks = 0;
    io.branch_refines = 0;
    io.branch_taken = 0;
    let r = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let mut s = Solver::new(io, effs);
        s.solve_inner();
    }));
    match r {
        Ok(()) => 0,
        Err(_) => -1,
    }
}

/// Pure-Rust entry for tests and benchmarks.
pub fn solve(io: &mut AllocIo, effs: &mut [EffN]) {
    io.n = effs.len() as i32;
    io.reach_moves = 0;
    io.two_horizon_bound = 0;
    io.coord_newton_moves = 0;
    io.paired_moves = 0;
    io.core_solves = 0;
    io.pair_moves = 0;
    io.pair_moves_k = [0; PAIR_KINDS];
    io.horizon_picks = 0;
    io.branch_refines = 0;
    io.branch_taken = 0;
    let mut s = Solver::new(io, effs);
    s.solve_inner();
}
