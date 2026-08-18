# GitHub Copilot Instructions

## Project Context

This is a .NET Aspire project. Target framework: net10.0. Package versions are centrally managed in Directory.Packages.props.

## Code Standards

- Use nullable reference types and implicit usings (both enabled)
- Follow SonarAnalyzer rules (code analysis warnings are treated as errors)
- Use file-scoped namespaces
- Don't specify package versions in .csproj files (use central package management)

## .NET Aspire Patterns

- AppHost.cs orchestrates all services using DistributedApplication builder
- ServiceDefaults project provides shared configuration for all services
- Services should call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`

## C# Method Constraints

- When reviewing generic C# methods constrained with `where T : struct`, do not assume a `T?` return plus `default` in a conditional yields null; verify the conditional expression's inferred runtime type, especially for invalid TryParse results.

## Diagnosis Guidelines

- During long-running diagnosis, provide visible progress updates and avoid leaving the user without activity for several minutes; switch approaches promptly when the current log source lacks required data.
- When a straightforward file edit is ready, apply it immediately instead of sending repeated progress-only messages.
