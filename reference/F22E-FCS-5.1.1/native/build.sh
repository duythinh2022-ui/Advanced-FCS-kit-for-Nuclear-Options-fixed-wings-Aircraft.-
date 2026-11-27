#!/bin/bash
# Builds the native allocator core. Needs Rust (built with 1.99.1) with the x86_64-pc-windows-gnu target and mingw-w64's gcc as
# its linker (Ubuntu: apt install gcc-mingw-w64-x86-64). No crates are downloaded.
#   build.sh            -> target/x86_64-pc-windows-gnu/release/F22E_FcsCore.dll   (the game; ships beside the plugin DLL)
#   build.sh linux      -> target/release/libF22E_FcsCore.so                       (the bench; regen.sh builds and copies it)
cd "$(dirname "$0")/fcs_core"
if [ "$1" = "linux" ]; then cargo build --release --offline
# 4.2.0: built with Rust 1.97.1 (Ubuntu's rustc-1.97 / rust-1.97-src), the Windows standard library compiled from source (-Zbuild-std,
# RUSTC_BOOTSTRAP) as the other FCS mods' cores; RUST_SRC points at a copy of library/ with std's windows-link dependency put back
# (std/Cargo.toml: [target.'cfg(any(windows, target_os = "cygwin"))'.dependencies.windows-link] path = "../windows_link"). With a
# rustup toolchain that has the x86_64-pc-windows-gnu target, set PLAIN=1 for the plain build.
elif [ -n "$PLAIN" ]; then CARGO_TARGET_X86_64_PC_WINDOWS_GNU_LINKER=${MINGW_GCC:-x86_64-w64-mingw32-gcc} cargo build --release --offline --target x86_64-pc-windows-gnu
else RUSTBIN=${RUSTBIN:-/usr/lib/rust-1.97/bin}; export PATH=$RUSTBIN:$PATH
  RUSTC_BOOTSTRAP=1 __CARGO_TESTS_ONLY_SRC_ROOT=${RUST_SRC:-/home/claude/rs197/lib/rustlib/src/rust/library} \
  CARGO_TARGET_X86_64_PC_WINDOWS_GNU_LINKER=${MINGW_GCC:-x86_64-w64-mingw32-gcc} \
  cargo build --release --target x86_64-pc-windows-gnu -Zbuild-std=std,panic_unwind -Zbuild-std-features=; fi
