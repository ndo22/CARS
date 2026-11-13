#!/usr/bin/env bash
# Zkompiluje všechny skripty proti skutečným referenčním knihovnám Unity 2021.3 (z NuGetu).
# UI/TextMeshPro a UnityEditor nejsou v balíčku - nahrazují je malé stuby v této složce.
set -euo pipefail
cd "$(dirname "$0")/../.."
WORK="${RUNNER_TEMP:-/tmp}/unity-refs"
if [ ! -d "$WORK/pkg" ]; then
  mkdir -p "$WORK"
  curl -sSL -o "$WORK/m.nupkg" https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  unzip -qo "$WORK/m.nupkg" -d "$WORK/pkg"
  # Soubory z nupkg nemají práva - bez toho je běžný uživatel (CI runner) nepřečte
  chmod -R u+rwX "$WORK/pkg"
fi
REFS=$(ls "$WORK"/pkg/lib/net45/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
mcs -warn:4 -target:library -out:"$WORK/CARS.dll" $REFS Tools/ci/UiStubs.cs $(find Assets/Scripts -name "*.cs")
mcs -target:library -out:"$WORK/CARS.Editor.dll" $REFS -r:"$WORK/CARS.dll" Tools/ci/EditorStubs.cs $(find Assets/Editor -name "*.cs")
echo "compile check OK"
