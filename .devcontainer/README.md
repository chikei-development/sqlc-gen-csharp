# Development Container for sqlc-gen-csharp

This devcontainer provides a complete development environment for the sqlc-gen-csharp project with all necessary dependencies pre-installed.

## What's Included

- **Base Image**: Microsoft .NET 8.0 development container
- **Tools**:
  - .NET 8.0 SDK
  - sqlc CLI tool (latest)
  - buf CLI tool (for protobuf generation)
  - make and build-essential
  - Docker-in-Docker support
  - GitHub CLI
- **VS Code Extensions**:
  - C# Dev Kit
  - Makefile Tools
  - Buf (Protocol Buffers)
  - Docker
  - JSON

## Getting Started

1. **Open in VS Code**: When you open this repository in VS Code, it will detect the devcontainer and offer to reopen in the container.

2. **Build the Project**:
   ```bash
   make dotnet-build
   ```

3. **Start Databases** (for testing):
   ```bash
   docker-compose up -d mysqldb postgresdb
   ```

4. **Run Tests**:
   ```bash
   make unit-tests
   ```

5. **Generate Code from SQL**:
   ```bash
   make sqlc-generate
   ```

## Available Make Targets

- `make dotnet-build` - Build the .NET solution
- `make unit-tests` - Run unit tests
- `make sqlc-generate` - Generate C# code from SQL schemas
- `make test-plugin` - Run the complete test suite
- `make generate-end2end-tests` - Generate end-to-end tests
- `make run-end2end-tests` - Run end-to-end tests

## Database Configuration

The devcontainer is configured to work with the included databases:

- **MySQL**: Available on port 3306
  - Database: `tests`
  - User: `root`
  - Password: (empty)

- **PostgreSQL**: Available on port 5432
  - Database: `tests`
  - User: `postgres`
  - Password: `postgres`

## Environment Variables

The following environment variables are pre-configured:
- `TESTS_DB=tests`
- `POSTGRES_USER=postgres`
- `POSTGRES_PASSWORD=postgres`

## Troubleshooting

If you encounter issues:

1. **Rebuild the container**: Command Palette → "Dev Containers: Rebuild Container"
2. **Check tool versions**:
   ```bash
   dotnet --version
   sqlc version
   buf --version
   ```
3. **Verify databases are running**:
   ```bash
   docker-compose ps
   ```