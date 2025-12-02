#!/bin/bash

set -e

echo "Setting up sqlc-gen-csharp development environment..."

# Update package lists
sudo apt-get update

# Install make and other build essentials
sudo apt-get install -y make build-essential wget curl unzip python3 python3-pip pipx

# Install pre-commit using pipx
echo "Installing pre-commit..."
pipx install pre-commit

# Install sqlc version 1.30.0 using Go (Go is provided by devcontainer feature)
echo "Installing sqlc v1.30.0..."
go install github.com/sqlc-dev/sqlc/cmd/sqlc@v1.30.0

# Install buf using Go
echo "Installing buf..."
go install github.com/bufbuild/buf/cmd/buf@v1.28.1

# Install yq using Go
echo "Installing yq..."
go install github.com/mikefarah/yq/v4@latest

# Install .NET workloads
echo "Installing .NET workloads..."
sudo dotnet workload install wasm-tools wasm-experimental wasi-experimental

# Install WASI SDK
echo "Installing WASI SDK..."
WASI_VERSION=24
WASI_VERSION_FULL=${WASI_VERSION}.0
cd /tmp
wget "https://github.com/WebAssembly/wasi-sdk/releases/download/wasi-sdk-${WASI_VERSION}/wasi-sdk-${WASI_VERSION_FULL}-x86_64-linux.tar.gz"
sudo tar xzf "wasi-sdk-${WASI_VERSION_FULL}-x86_64-linux.tar.gz" -C /opt
sudo ln -sf "/opt/wasi-sdk-${WASI_VERSION_FULL}-x86_64-linux" /opt/wasi-sdk
cd /workspaces/sqlc-gen-csharp

# Verify installations
echo "Verifying installations..."
dotnet --version
dotnet workload list
sqlc version
buf --version
yq --version
make --version
pre-commit --version
echo "WASI SDK: $(/opt/wasi-sdk/bin/clang --version | head -1)"

# Set up .NET tools
echo "Restoring .NET tools and packages..."
dotnet restore

# Set up pre-commit hooks
echo "Setting up pre-commit hooks..."
git config --global --add safe.directory /workspaces/sqlc-gen-csharp
pre-commit install

echo "Development environment setup complete!"
echo ""
echo "Available commands:"
echo "  make dotnet-build       - Build the project"
echo "  make unit-tests         - Run unit tests"
echo "  make sqlc-generate      - Generate code from SQL"
echo "  make test-plugin        - Run full test suite"
