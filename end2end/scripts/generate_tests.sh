#!/usr/bin/env bash

set -ex

TARGETS=(
  "MySqlConnectorTester"
  "MySqlConnectorDapperTester"
  "NpgsqlTester"
  "NpgsqlDapperTester"
  "SqliteTester"
  "SqliteDapperTester"
)
  
generate() {
  export TEST_CLASS_NAME="$1"
  local TEST_FILENAME="${TEST_CLASS_NAME}.generated.cs"
  echo "generating EndToEndTests/$TEST_FILENAME..."
  dotnet run --project ./end2end/EndToEndScaffold/EndToEndScaffold.csproj > ./end2end/EndToEndTests/"$TEST_FILENAME"
}

for target in "${TARGETS[@]}"; do
    generate "$target"
done