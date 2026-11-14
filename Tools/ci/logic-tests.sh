#!/usr/bin/env bash
# Spustí EditMode testy bez Unity: herní logika + náhrada Unity API (UnityShim.cs) + minimální NUnit.
set -euo pipefail
cd "$(dirname "$0")/../.."
OUT="${RUNNER_TEMP:-/tmp}/cars-logic-tests.exe"
S=Assets/Scripts
mcs -out:"$OUT" Tools/ci/UnityShim.cs Tools/ci/NUnitShim.cs Tools/ci/TestRunner.cs \
  $S/TimeFormat.cs $S/CarController.cs $S/AICar.cs $S/RacerBase.cs \
  $S/Core/TrackMarkers.cs $S/Core/Achievements.cs \
  $S/Effects/LampCuller.cs $S/Effects/Ghost.cs $S/Effects/ProceduralAudio.cs \
  $S/LowPoly/MeshBuilder.cs $S/LowPoly/TrackGenerator.cs $S/LowPoly/TrackTheme.cs $S/LowPoly/TrackLibrary.cs \
  $S/LowPoly/CustomTracks.cs $S/LowPoly/RaceSetup.cs $S/LowPoly/ObstacleCourse.cs $S/LowPoly/ChaseLevels.cs $S/Effects/CourseObjects.cs $S/LowPoly/LowPolyCarFactory.cs $S/LowPoly/Spinner.cs \
  Assets/Tests/EditMode/CoreTests.cs
mono "$OUT"
