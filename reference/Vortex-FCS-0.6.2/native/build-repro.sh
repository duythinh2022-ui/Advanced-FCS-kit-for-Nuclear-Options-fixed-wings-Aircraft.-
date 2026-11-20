#!/bin/bash
# Reproducible build of the native core, Vortex_FcsCore.dll (the DLL embedded in Vortex_FS20_FCS.dll).
#
# Same source + same toolchain + same linker = the same bytes, on any machine and in any folder:
#   - the Rust version and the Windows target are pinned in fcs_core/rust-toolchain.toml (rustup installs them);
#   - Cargo.lock is used as is (--locked); the core has no dependencies;
#   - the build folder, CARGO_HOME and the toolchain's own path are remapped, so no local path ends up in the DLL;
#   - the linker writes no timestamp into the PE header.
# The linker (mingw-w64) also decides bytes, so the reference environment is the Docker image CI uses:
#
#   docker run --rm -v "$PWD":/src -w /src rust:1.97.0-bookworm bash -c \
#     "apt-get update -qq && apt-get install -y -qq gcc-mingw-w64-x86-64 >/dev/null && bash native/build-repro.sh"
#
# It prints the SHA-256 of the DLL it built and of the committed native/prebuilt/Vortex_FcsCore.dll; they must be equal.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
cd "$here/fcs_core"
src="$(pwd)"
cargo_home="${CARGO_HOME:-$HOME/.cargo}"
sysroot="$(rustc --print sysroot)"
export CARGO_TARGET_X86_64_PC_WINDOWS_GNU_LINKER="${MINGW_GCC:-x86_64-w64-mingw32-gcc}"
export CARGO_INCREMENTAL=0
export RUSTFLAGS="--remap-path-prefix=$src=/build/fcs_core --remap-path-prefix=$cargo_home=/cargo --remap-path-prefix=$sysroot=/rust -C link-arg=-Wl,--no-insert-timestamp"
cargo build --release --locked --target x86_64-pc-windows-gnu
out="target/x86_64-pc-windows-gnu/release/Vortex_FcsCore.dll"
echo "rustc:  $(rustc --version)"
echo "linker: $("$CARGO_TARGET_X86_64_PC_WINDOWS_GNU_LINKER" --version | head -1)"
sha256sum "$out" | sed "s|  .*|  built     $out|"
sha256sum "$here/prebuilt/Vortex_FcsCore.dll" | sed "s|  .*|  committed native/prebuilt/Vortex_FcsCore.dll|"
