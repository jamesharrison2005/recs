# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build & Run
- Build solution: `dotnet build`
- Run API: `dotnet run --project src/Recs.Api`
- Run API with watch mode: `dotnet watch --project src/Recs.Api`

### Testing
- Run all tests: `dotnet test`
- Run a specific test project: `dotnet test tests/Recs.Tests`
- Run a single test by method name: `dotnet test --filter "Name=Test1"`
- Run tests in a specific class: `dotnet test --filter "FullyQualifiedName~Recs.Tests.UnitTest1"`
- Run tests with coverage: `dotnet test --collect:"XPlat Code Coverage"`

## Architecture

This is a .NET 10 recommendation system service structured with Clean Architecture and powered by ML.NET.

Solution structure defined in `Recs.slnx`:

- **`src/Recs.Domain`**: Core domain entities and business logic with zero external dependencies.
- **`src/Recs.Application`**: Application use cases, interfaces, and orchestrations. References `Recs.Domain`.
- **`src/Recs.Infrastructure`**: Persistence, data access, and external system integrations. References `Recs.Application`.
- **`src/Recs.ML`**: Recommendation models, training pipelines, and scoring using ML.NET (`Microsoft.ML` and `Microsoft.ML.Recommender`). References `Recs.Application`.
- **`src/Recs.Api`**: ASP.NET Core Minimal API entry point and dependency injection composition root. References `Recs.Application`, `Recs.Infrastructure`, and `Recs.ML`.
- **`tests/Recs.Tests`**: Unit and integration tests using xUnit. References `Recs.Domain`, `Recs.Application`, and `Recs.ML`.

## Decisions
- Hybrid model: matrix factorization (collaborative) plus content-based similarity
- Cold start is a core requirement, not a stretch goal
- Data: Yelp subset first, then real Isle of Man venues and events
- Location and travel feasibility are a planned differentiator
- Endpoints: /recommendations/{userId}, /rate, /items

## Conventions
- Small, focused commits; tests alongside new code
- Ask before adding NuGet packages
