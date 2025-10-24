SHELL 		:= /bin/bash
PWD 		:= $(shell pwd)

dotnet-build:
	dotnet build

.PHONY: unit-tests
unit-tests:
	dotnet test unit-tests/RepositoryTests
	sqlc generate -f sqlc.unit.test.yaml
	dotnet test unit-tests/CodegenTests

generate-end2end-tests:
	./end2end/scripts/generate_tests.sh
    
run-end2end-tests:
	./end2end/scripts/run_tests.sh

# process type plugin
dotnet-publish-process:
	dotnet publish LocalRunner -c release --output dist/

sync-sqlc-options:
	./scripts/sync_sqlc_options.sh

.PHONY: pre-commit
pre-commit:
	@echo "This script is intended to automate all of the garbage you need to do before committing. Please only run it if *all* files are currently staged for commit (i.e. git add . has already been run)."
	@echo "If you have unstaged changes, please stash them first (git stash)."
	@echo "This will run:"
	@echo "  1) pre-commit run (dotnet format, yaml linting, etc.)"
	@echo "  2) if there are any changes, git add ."
	@echo "  3) the makefile targets: sqlc-generate, unit-tests, generate-end2end-tests, run-end2end-tests"
	@echo "  4) if there are any changes, git add ."
	@echo "This all sound good? (y/n)"
	@read ans; \
	if [ "$$ans" != "y" ]; then \
		echo "Aborting."; \
		exit 1; \
	fi

	@echo "Checking for unstaged changes..."
	@if [ -n "$$(git diff --stat)" ]; then \
		echo "You have unstaged changes. Please stash or stage them first."; \
		exit 1; \
	fi

	pre-commit run || exit 1
	@if [ -n "$$(git status --porcelain)" ]; then \
		git add . || exit 1; \
	fi
	$(MAKE) sqlc-generate || exit 1
	$(MAKE) unit-tests || exit 1
	$(MAKE) generate-end2end-tests || exit 1
	$(MAKE) run-end2end-tests || exit 1
	@if [ -n "$$(git status --porcelain)" ]; then \
		git add . || exit 1; \
	fi

sqlc-generate-requests: dotnet-publish-process
	SQLCCACHE=./; sqlc -f sqlc.request.generated.yaml generate

sqlc-generate: sync-sqlc-options dotnet-publish-process sqlc-generate-requests
	SQLCCACHE=./; sqlc -f sqlc.local.generated.yaml generate

test-plugin: unit-tests sqlc-generate generate-end2end-tests dotnet-build run-end2end-tests

# WASM type plugin
setup-ci-wasm-plugin:
	dotnet publish WasmRunner -c release --output dist/
	./scripts/wasm/copy_plugin_to.sh dist
	./scripts/wasm/update_sha.sh sqlc.ci.yaml

# Manual
generate-protobuf:
	./scripts/generate_protobuf.sh