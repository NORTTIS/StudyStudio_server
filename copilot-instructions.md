# Copilot Instructions - StudioStudio Backend Project

## ?? Project Overview
- **Framework:** ASP.NET Core 8.0 (Web API)
- **Database:** PostgreSQL with Entity Framework Core
- **Architecture:** Layered Architecture (Controller ? Service ? Repository ? DbContext)
- **Authentication:** JWT Bearer Token
- **Localization:** Multi-language support (Vietnamese & English)

---

## ?? Project Structure

## 📁 Project Structure

```
StudioStudio_Server/
├── Controllers/                # API Endpoints
├── Services/                   # Business Logic Layer
│   ├── Interfaces/             # Service Interfaces
│   └── *.cs                    # Service Implementations
├── Repositories/               # Data Access Layer
│   ├── Interfaces/             # Repository Interfaces
│   └── *.cs                    # Repository Implementations
├── Models/
│   ├── DTOs/                   # Data Transfer Objects
│   │   ├── Request/            # API Request Models
│   │   └── Response/           # API Response Models
│   ├── Entities/               # Database Entities
│   └── Enums/                  # Enumerations
├── Data/                       # DbContext
├── Exceptions/                 # Custom Exceptions & Error Codes
├── Filters/                    # Action Filters
├── Middlewares/                # Custom Middlewares
├── Resources/                  # Localization JSON files
│   └── Errors/
│       ├── errors.vi.json
│       └── errors.en.json
├── Configurations/             # Configuration classes
└── Docs/                       # API Documentation
```

## ??? Architecture Patterns

### **Layered Architecture (MUST FOLLOW)**

```
Controller ? Service ? Repository ? DbContext ? Database
```

#### ? **NEVER DO THIS:**
```csharp
// Controller directly accessing Repository
public class MyController {
    private readonly IMyRepository _repository;
}

// Service directly accessing DbContext
public class MyService {
    private readonly StudioDbContext _db;
    
    public async Task DoSomething() {
        var data = await _db.MyTable.ToListAsync(); // ? WRONG
    }
}
```

#### ? **CORRECT WAY:**
```csharp
// Controller ? Service
public class MyController {
    private readonly IMyService _service;
}

// Service ? Repository
public class MyService : IMyService {
    private readonly IMyRepository _repository;
}

// Repository ? DbContext
public class MyRepository : IMyRepository {
    private readonly StudioDbContext _context;
}
```

---

## ?? Naming Conventions

### **Interfaces**
- Prefix with `I`
- PascalCase
- Examples: `IUserService`, `IGroupRepository`, `IMessageService`

### **Classes**
- PascalCase
- Examples: `UserService`, `GroupRepository`, `GroupController`

### **Methods**
- PascalCase
- Async methods MUST end with `Async`
- Examples: `GetUserAsync()`, `CreateGroupAsync()`, `DeleteAsync()`

### **Properties**
- PascalCase
- Examples: `UserId`, `GroupName`, `CreatedAt`

### **Private Fields**
- Prefix with `_`
- camelCase
- Examples: `_userRepository`, `_logger`, `_messageService`

### **Parameters & Local Variables**
- camelCase
- Examples: `userId`, `groupName`, `request`

### **Constants**
- PascalCase
- Examples: `ErrorCodes.UserNotFound`, `ErrorCodes.ValidationRequiredField`

---

## ?? Code Style Conventions

### **Indentation**
- Use **4 spaces** (not tabs)
- Indent switch case contents

### **Braces**
- Always use braces for control structures
- Opening brace on new line
```csharp
// ? CORRECT
if (condition)
{
    DoSomething();
}

// ? WRONG
if (condition) DoSomething();
```

### **var Keyword**
- **DO NOT** use `var` for built-in types
- **DO NOT** use `var` when type is not apparent
```csharp
// ? CORRECT
int count = 10;
string name = "test";
List<User> users = new List<User>();

// ? WRONG
var count = 10;
var name = "test";
```

### **Nullable Reference Types**
- Use `?` for nullable types
- Set explicit default values for clarity
```csharp
// ? CORRECT
public Guid? StudioId { get; set; } = null;
public string? Description { get; set; }

// Less preferred (but acceptable)
public Guid? StudioId { get; set; }
```

### **String Initialization**
```csharp
// ? CORRECT
public string Name { get; set; } = string.Empty;

// ? WRONG
public string Name { get; set; } = "";
```

---

## ?? Error Handling

### **Error Codes**
- All error codes defined in `ErrorCodes.cs`
- Format: `CATEGORY###` (e.g., `AUTH001`, `USER001`, `GROUP001`)
- Categories: `AUTH`, `USER`, `GROUP`, `TASK`, `VALIDATION`, `SUCCESS`, `SYS`

### **Custom Exceptions**
```csharp
// Always use AppException with error code
throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
```

### **Exception Handling in Controllers**
```csharp
// ? WRONG - Don't catch exceptions in controllers
try {
    var result = await _service.DoSomething();
} catch (Exception ex) {
    return BadRequest(ex.Message);
}

// ? CORRECT - Let middleware handle exceptions
var result = await _service.DoSomething();
return Ok(ApiResponse<T>.Success(code, message, result));
```

---

## ?? Localization

### **Message Files**
- `Resources/Errors/errors.vi.json` (Vietnamese)
- `Resources/Errors/errors.en.json` (English)

### **Adding New Messages**
1. Add error code to `ErrorCodes.cs`
2. Add message to BOTH `errors.vi.json` and `errors.en.json`
3. Use `IMessageService.GetMessage(code)` to retrieve

```csharp
// ? CORRECT
var message = _messageService.GetMessage(ErrorCodes.SuccessCreateGroup);
return Ok(ApiResponse<T>.Success(ErrorCodes.SuccessCreateGroup, message, data));
```

---

## ?? Authentication & Authorization

### **All API endpoints MUST:**
```csharp
[Authorize]
public async Task<ActionResult> MyEndpoint()
{
    // 1. Extract userId from JWT
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
    {
        throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
    }

    // 2. Check if admin (admins not allowed for user APIs)
    var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
    var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
    if (isAdmin)
    {
        throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
    }

    // 3. Call service
    var result = await _service.DoSomething(userId);
    return Ok(result);
}
```

---

## ?? DTOs & Models

### **Request DTOs**
- Located in `Models/DTOs/Request/`
- Suffix with `Request`
- Use DataAnnotations for validation
```csharp
public class CreateGroupRequest
{
    public Guid? StudioId { get; set; } = null;

    [Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
    [StringLength(100, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
    public string GroupName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = ErrorCodes.ValidationRequiredField)]
    public string? Description { get; set; }
}
```

### **Response DTOs**
- Located in `Models/DTOs/Response/`
- Suffix with `Response`
- Use `ApiResponse<T>` wrapper
```csharp
public class CreateGroupResponse
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? Description { get; set; }
    // ...
}
```

### **API Response Format**
```csharp
// Success
return Ok(ApiResponse<T>.Success(code, message, data));

// Error (handled by middleware)
throw new AppException(errorCode, httpStatusCode);
```

---

## ??? Database

### **Entity Framework Conventions**
- Use `AsNoTracking()` for read-only queries
- Always use async methods (`ToListAsync()`, `FirstOrDefaultAsync()`, etc.)
- Use `Include()` for eager loading related entities

```csharp
// ? CORRECT
public async Task<List<Group>> GetUserGroupsAsync(Guid userId)
{
    return await _context.Groups
        .Where(g => g.Participants.Any(p => p.UserId == userId) && g.IsActive)
        .Include(g => g.Participants)
        .AsNoTracking()
        .ToListAsync();
}
```

### **Soft Delete**
- Never hard delete records
- Use `IsActive` flag
```csharp
public async Task DeleteAsync(Group group)
{
    group.IsActive = false;
    group.UpdatedAt = DateTime.UtcNow;
    _context.Groups.Update(group);
    await _context.SaveChangesAsync();
}
```

---

## ?? Validation

### **Request Validation**
- Use DataAnnotations in DTOs
- Custom validation in Services (business rules)
- Use error codes from `ErrorCodes.cs`

```csharp
// DTO Validation
[Required(ErrorMessage = ErrorCodes.ValidationRequiredField)]
[StringLength(100, MinimumLength = 1, ErrorMessage = ErrorCodes.ValidationRequiredField)]
public string GroupName { get; set; } = string.Empty;

// Service Validation
if (currentGroupCount >= groupLimit)
{
    throw new AppException(ErrorCodes.GroupLimitReached, StatusCodes.Status403Forbidden);
}
```

---

## ?? Testing

### **Repository Tests**
- Use In-Memory Database
- Test data access logic only

### **Service Tests**
- Mock repositories
- Test business logic

---

## ?? Documentation

### **XML Comments**
- Add for public APIs
- Describe parameters and return values

### **API Documentation**
- Create markdown files in `Docs/` folder
- Include request/response examples
- Document error codes

---

## ?? Common Anti-Patterns to Avoid

### ? **1. Service accessing DbContext directly**
```csharp
// WRONG
public class MyService {
    private readonly StudioDbContext _db;
    public async Task DoSomething() {
        var users = await _db.Users.ToListAsync();
    }
}
```

### ? **2. Controller accessing Repository**
```csharp
// WRONG
public class MyController {
    private readonly IUserRepository _repository;
}
```

### ? **3. Using try-catch in Controllers**
```csharp
// WRONG - Let middleware handle exceptions
try {
    var result = await _service.DoSomething();
} catch (Exception ex) {
    return BadRequest(ex.Message);
}
```

### ? **4. Hard Delete**
```csharp
// WRONG
_context.Groups.Remove(group);

// CORRECT
group.IsActive = false;
_context.Groups.Update(group);
```

### ? **5. Missing Async Suffix**
```csharp
// WRONG
public async Task<User> GetUser(Guid id)

// CORRECT
public async Task<User> GetUserAsync(Guid id)
```

---

## ? Code Review Checklist

Before submitting code, ensure:

- [ ] Follows layered architecture (Controller ? Service ? Repository ? DbContext)
- [ ] All async methods have `Async` suffix
- [ ] Error codes added to `ErrorCodes.cs` and localization files
- [ ] No direct DbContext access from Services
- [ ] Proper exception handling with `AppException`
- [ ] DTOs follow naming conventions (Request/Response suffix)
- [ ] Soft delete instead of hard delete
- [ ] Authorization checks in controllers
- [ ] XML documentation for public APIs
- [ ] Tests written (if applicable)

---

## ?? Best Practices

1. **Always use Repository Pattern** - No direct DbContext in Services
2. **Use Dependency Injection** - Constructor injection for all dependencies
3. **Async All The Way** - Use async/await consistently
4. **Soft Delete** - Never hard delete records
5. **Centralized Error Handling** - Use `AppException` and middleware
6. **Localization** - Support multiple languages from day one
7. **Explicit Nullability** - Always specify nullable types clearly
8. **Meaningful Names** - Self-documenting code
9. **SOLID Principles** - Follow Single Responsibility Principle
10. **Test Your Code** - Write unit tests for critical logic

---

## ?? Related Files

- `.editorconfig` - Code formatting rules
- `ErrorCodes.cs` - All error code constants
- `errors.vi.json` / `errors.en.json` - Localized messages
- `StudioDbContext.cs` - Database context configuration

---

**Last Updated:** 2024
**Maintained By:** StudioStudio Team
