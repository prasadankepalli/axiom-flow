# Contributing to Axiom-Flow

First off, thank you for considering contributing to Axiom-Flow! It's people like you that make Axiom-Flow a great tool for the AI community.

## 📜 Code of Conduct

By participating in this project, you agree to abide by our Code of Conduct. (Standard contributor covenant applies).

## 🛠 Development Workflow

1.  **Fork the Repository:** Create a personal fork of the project on GitHub.
2.  **Clone the Fork:** `git clone https://github.com/your-username/AxiomFlow.git`
3.  **Create a Branch:** `git checkout -b feature/your-feature-name`
4.  **Implement & Test:** 
    - Follow **CLEAN Architecture** and **SOLID Principles**.
    - Ensure all projects build: `dotnet build AxiomFlow.sln`
    - Add unit tests for all new logic.
    - Run tests: `dotnet test AxiomFlow.sln`
5.  **Commit Changes:** Use descriptive commit messages.
6.  **Push & Pull Request:** Push to your fork and submit a PR to the `main` branch.

## 🏗 Coding Standards

- **Nullable Reference Types:** Enabled and enforced.
- **Async/Await:** Use `Task` based asynchronous patterns for all I/O or AI calls.
- **Immutability:** Use `record` for data models and results.
- **Dependency Injection:** All services should be registered via `IServiceCollection` extensions.
- **Logging:** Use `ILogger` with structured JSON logging patterns.
- **Telemetry:** Use `ActivitySource` for significant operations to enable OpenTelemetry tracing.

## 🧪 Testing Requirements

- **Unit Tests:** Mandatory for all new `Core` and `Evaluator` logic.
- **Code Coverage:** Aim for high coverage on validation logic.
- **Mocking:** Use `Moq` for external dependencies (APIs, DBs, LLMs).

## 📖 Documentation

- Update the `README.md` if you add new high-level features.
- Update `DEVELOPER_GUIDE.md` if you add new extensibility points or core interfaces.
- Use XML comments on all public interfaces and models.

## 🚀 Pull Request Process

1.  Ensure the build and tests pass locally.
2.  The GitHub Action `axiom-gate.yml` will run on your PR. It must pass for the PR to be merged.
3.  A maintainer will review your code and may suggest changes.
4.  Once approved, your PR will be merged!
