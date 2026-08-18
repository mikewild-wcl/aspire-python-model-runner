# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET Aspire project.

## Architecture

The solution consists of two projects:

- **Aspire.PythonModelRunner.AppHost**: The Aspire orchestrator project that coordinates all services. Uses the latest `Aspire.AppHost.Sdk` SDK. Entry point is `AppHost.cs` which creates a minimal `DistributedApplication` with no resources configured by default.

- **Aspire.PythonModelRunner.ServiceDefaults**: A shared library project that provides common Aspire service configuration (OpenTelemetry, service discovery, resilience, health checks). This project should be referenced by all service projects in an Aspire application. The main extension methods are in `Extensions.cs`.

### Key Architectural Patterns

- **Central Package Management**: Package versions are managed centrally in `Directory.Packages.props` with `ManagePackageVersionsCentrally` enabled.

- **Service Defaults Pattern**: The ServiceDefaults project extends `Microsoft.Extensions.Hosting` namespace with extension methods (`AddServiceDefaults`, `ConfigureOpenTelemetry`, `MapDefaultEndpoints`) to configure common Aspire features across all services.

- **Minimal AppHost**: The AppHost project is intentionally empty - new services, containers, and resources should be added to `AppHost.cs` as needed.

- **Code Quality Enforcement**: `Directory.Build.props` enables strict analysis mode with SonarAnalyzer, treating code analysis warnings as errors (`CodeAnalysisTreatWarningsAsErrors=true`).

## Build and Run Commands

Build the solution:
```
dotnet build src/Aspire.PythonModelRunner.slnx
```

Run the AppHost (starts the Aspire dashboard and orchestrates resources):
```
dotnet run --project src/Aspire.PythonModelRunner.AppHost
```

The AppHost will launch the Aspire Dashboard at the URLs configured in `launchSettings.json` (default: https://aspire_pythonmodelrunner.dev.localhost:17232).

## Service Defaults Configuration

When adding new service projects to an Aspire application created from this template:

1. Reference the ServiceDefaults project
2. Call `builder.AddServiceDefaults()` in the service's `Program.cs`
3. Call `app.MapDefaultEndpoints()` on WebApplication instances

The ServiceDefaults project provides:
- OpenTelemetry with metrics, tracing, and logging (exports to OTLP endpoint if configured)
- Service discovery for HTTP clients
- Standard resilience handlers for HTTP clients
- Health check endpoints at `/health` and `/alive` (development only)
- ASP.NET Core instrumentation excluding health check paths

## Framework and Packages

Minimum versions:
- - Target framework: `net10.0`
- Aspire SDK: `13.1.0`
- OpenTelemetry packages: `1.14.0`
- Microsoft Aspire extensions: `10.1.0`

## Code Quality

- Nullable reference types enabled
- Implicit usings enabled
- Latest analysis level with "All" mode
- SonarAnalyzer.CSharp enforces additional rules
- EditorConfig at root defines coding standards
