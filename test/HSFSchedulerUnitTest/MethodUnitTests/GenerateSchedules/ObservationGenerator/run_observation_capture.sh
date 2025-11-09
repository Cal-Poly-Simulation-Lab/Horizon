#!/bin/bash
# Build and run observation capture
# This generates baseline data but does NOT run as part of test suite

cd /Users/jason/source/Horizon

echo "═══════════════════════════════════════════════════════"
echo "  Building Test Project"
echo "═══════════════════════════════════════════════════════"

dotnet build test/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj

if [ $? -ne 0 ]; then
    echo "Build failed!"
    exit 1
fi

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  Running Observation Data Capture"
echo "═══════════════════════════════════════════════════════"
echo ""

# Call the static Main method via a simple wrapper test
dotnet test test/HSFSchedulerUnitTest/HSFSchedulerUnitTest.csproj \
    --filter "FullyQualifiedName~ObservationRunner" \
    --logger "console;verbosity=normal"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  Done! Check ObservationGenerator/Output/ for files"
echo "═══════════════════════════════════════════════════════"

