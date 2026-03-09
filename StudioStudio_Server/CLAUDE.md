# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

StudioStudio is an ASP.NET Core 8.0 Web API backend for a collaborative study management platform. It provides user authentication, studio/group management, task tracking, real-time messaging, AI-powered document features, and subscription-based payments.

## Tech Stack

- **Framework**: ASP.NET Core 8.0
- **Database**: PostgreSQL with Entity Framework Core 8.0
- **Cache**: Redis (StackExchange.Redis)
- **Authentication**: JWT Bearer + Google OAuth
- **Real-time**: SignalR
- **External Services**: Google Gemini (AI), Backblaze B2 (storage), Qdrant (vector DB), PayOS (payments), SMTP (email)

## Build & Run

```bash
# Build the project
dotnet build

# Run in development
dotnet run

# Run with hot reload
dotnet watch

# Run with Docker
docker build -t studystudio .
docker run -p 8080:8080 studystudio
```

## Database

- Migrations are managed with EF Core
- Run migrations on startup via `db.Database.Migrate()` in Program.cs
- Seed data is loaded via `ISeederService.SeedInitialDataAsync()`

## Architecture

The project follows **Service-Repository pattern**:

```
Controllers/     → HTTP endpoints, request/response handling
Services/        → Business logic with interface-based DI
Repositories/    → Data access layer
Data/            → EF Core DbContext and entity configurations
Middlewares/     → Cross-cutting concerns (exception, rate limit, token validation)
Hubs/            → SignalR real-time hubs
Models/          → DTOs and Entities
Configurations/  → Typed configuration options
```

## Key Services

- **AuthService**: JWT token generation, Google OAuth, refresh tokens
- **GroupService/StudioService**: Core domain logic for studios and groups
- **TaskService**: Personal and group task management
- **AIService**: Document embedding and AI chat using Gemini
- **DocumentService**: File upload/download via Backblaze
- **PaymentService**: PayOS integration for subscriptions
- **EmbeddingQueue/DeleteQueue**: Background services for async document processing

## SignalR Hubs

- `/hubs/group-discuss` - Real-time group messaging
- `/hubs/task-comment` - Real-time task comments

## Configuration

All configuration is in `appsettings.json`. Key sections:
- `ConnectionStrings` - PostgreSQL and Redis
- `JWT` - Token settings (issuer, audience, expiry)
- `Google` - OAuth client ID
- `Backblaze` - B2 storage credentials
- `Qdrant` - Vector database settings
- `Gemini` - AI API settings
- `PayOS` - Payment gateway credentials

## API Documentation

Swagger UI is available at `/swagger` in Development mode.

## Important Patterns

1. **DTO Request/Response**: All endpoints use DTOs in `Models/DTOs/Request` and `Models/DTOs/Response`
2. **Validation**: Uses `ValidationFilter` with FluentValidation
3. **Error Handling**: Custom `ExceptionHandlingMiddleware` returns standardized `ApiResponse`
4. **Service Interfaces**: All services follow interface-based DI pattern
5. **Background Queues**: Embedding and deletion operations use in-memory queues with hosted services
