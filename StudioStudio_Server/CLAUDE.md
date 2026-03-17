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

## Error Handling & Exceptions

### Custom Exception Flow

```
Request → Controller → Service → [throw AppException] → ExceptionHandlingMiddleware → ApiResponse
```

- **AppException** (`Exceptions/AppException.cs`): Base exception with `Code` and `HttpStatus`
- **ErrorCodes** (`Exceptions/ErrorCodes.cs`): Centralized error code constants

### Error Code Categories

| Prefix | Category | Examples |
|--------|----------|----------|
| `AUTH` | Authentication | `AUTH001` - Invalid credential, `AUTH002` - Token expired, `AUTH003` - Forbidden |
| `USER` | User | `USER001` - Not found, `USER002` - Already exists |
| `GROUP` | Group/Studio | `GROUP001` - Not found, `GROUP003` - Limit reached |
| `TASK` | Task | `TASK001` - Not found, `TASK002` - Permission denied |
| `VALIDATION` | Input validation | `VALIDATION001` - Invalid email, `VALIDATION002` - Invalid password |
| `PAYMENT` | Payment | `PAYMENT001` - Plan not found, `PAYMENT003` - Payment not found |
| `SUCCESS` | Success responses | `SUCCESS001` - Register success, `SUCCESS010` - Get data success |
| `SYS` | System | `SYS001` - Unexpected error |

### Response Format

All API responses use `ApiResponse<T>`:
```json
{
  "status": "success" | "error",
  "code": "SUCCESS010",
  "message": "Data retrieved successfully",
  "data": { ... }
}
```

### Throwing Exceptions in Services

```csharp
// Simple throw
throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);

// With validation
if (user == null)
    throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
```

## Code Comments

### Service Comments Pattern

Use XML summary comments for all public methods:

```csharp
/// <summary>
/// Get user by ID
/// Validate: User must exist and not be deleted
/// Returns: User information if found
/// </summary>
/// <param name="userId">User's unique identifier</param>
public async Task<UserResponse> GetByIdAsync(Guid userId)
```

### Controller Comments Pattern

```csharp
/// <summary>
/// Get user profile
/// Authenticate and get userId from JWT token
/// Validate: User must exist and not be admin
/// </summary>
/// <param name="cancellationToken">Cancellation token</param>
[Authorize]
[HttpGet("profile")]
public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
```

### Key Comment Elements

1. **`<summary>`**: Brief description of what the method does
2. **Validate**: Preconditions that cause exceptions (e.g., "User must exist")
3. **Returns**: What the method returns on success
4. **Use case**: When this method is typically called

## Background Services

- **EmbeddingBackgroundService**: Processes document embeddings asynchronously via `EmbeddingQueue`
- **DeleteQueue**: Handles async document deletion from vector DB and storage

## Health Checks

- `/health` endpoint with checks for Database, Redis, and External Services

## Cache Configuration

Set `Cache:Provider` in appsettings to `"Memory"` (dev) or `"Redis"` (production)
