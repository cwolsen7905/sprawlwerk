#!/usr/bin/env bash
# Fails if the engine-agnostic simulation (src/SprawlWerk.Sim) depends on
# Godot: in its C# code, in its project files, or in its resolved packages.
# Run from anywhere; CI runs it on every push. Usage: tools/check-sim-no-godot.sh [sim-dir]
set -euo pipefail

cd "$(dirname "$0")/.."
sim="${1:-src/SprawlWerk.Sim}"
fail=0

report() {
  echo "check-sim-no-godot: FAILED: $1" >&2
  fail=1
}

# 1. C# code: using directives, qualified names (Godot.Node, GodotSharp.x)
#    and global:: aliases. Plain prose such as "no Godot here" is allowed.
code_pattern='using[[:space:]]+(static[[:space:]]+)?Godot([.;[:space:]]|$)|global::Godot|(^|[^A-Za-z0-9_])Godot(Sharp)?\.[A-Za-z_]'
if grep -rnE --include='*.cs' --exclude-dir=bin --exclude-dir=obj "$code_pattern" "$sim"; then
  report "C# code in $sim references Godot (lines above)"
fi

# 2. Project files: a Godot SDK, package or reference, or a reference to
#    the game project.
proj_pattern='(Sdk|Include|Update)="[^"]*Godot|ProjectReference[^>]*game[/\\]'
if grep -rnE --exclude-dir=bin --exclude-dir=obj \
     --include='*.csproj' --include='*.props' --include='*.targets' \
     "$proj_pattern" "$sim"; then
  report "project files in $sim reference Godot or the game (lines above)"
fi

# 3. Resolved packages, including transitive ones and any added from a
#    Directory.Build.props outside $sim. dotnet restores first if needed.
if ! packages="$(dotnet list "$sim" package --include-transitive 2>&1)"; then
  echo "$packages" >&2
  report "could not list packages for $sim"
elif grep -iE '>[[:space:]]*Godot' <<<"$packages"; then
  report "$sim resolves a Godot package (lines above)"
fi

if [ "$fail" -ne 0 ]; then
  exit 1
fi
echo "check-sim-no-godot: ok ($sim has no Godot reference)"
