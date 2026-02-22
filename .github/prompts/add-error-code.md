# Add Error Code and Localization

Add a new error code with localized messages.

## Input Required:
- Error category (AUTH, USER, GROUP, TASK, VALIDATION, SUCCESS, etc.)
- Error description
- Vietnamese message
- English message

## Expected Output:

### 1. Add Error Code Constant
File: `Exceptions/ErrorCodes.cs`

Find the appropriate category section and add:
```csharp
// {CATEGORY}
public const string {ErrorName} = "{CATEGORY}###";
```

### 2. Add Vietnamese Message
File: `Resources/Errors/errors.vi.json`

```json
{
  "...": "...",
  "{CATEGORY}###": "Vietnamese error message here",
  "...": "..."
}
```

### 3. Add English Message
File: `Resources/Errors/errors.en.json`

```json
{
  "...": "...",
  "{CATEGORY}###": "English error message here",
  "...": "..."
}
```

## Error Code Categories:

### AUTH (AUTH001-AUTH099)
Authentication and authorization errors
```csharp
public const string AuthInvalidCredential = "AUTH001";
public const string AuthTokenExpired = "AUTH002";
public const string AuthForbidden = "AUTH003";
```

### USER (USER001-USER099)
User-related errors
```csharp
public const string UserNotFound = "USER001";
public const string UserAlreadyExist = "USER002";
```

### GROUP (GROUP001-GROUP099)
Group-related errors
```csharp
public const string GroupNotFound = "GROUP001";
public const string GroupNameAlreadyExists = "GROUP002";
public const string GroupLimitReached = "GROUP003";
```

### TASK (TASK001-TASK099)
Task-related errors
```csharp
public const string TaskNotFound = "TASK001";
public const string TaskPermissionDenied = "TASK002";
```

### VALIDATION (VALIDATION001-VALIDATION099)
Input validation errors
```csharp
public const string ValidationInvalidEmail = "VALIDATION001";
public const string ValidationInvalidPassword = "VALIDATION002";
public const string ValidationRequiredField = "VALIDATION004";
```

### SUCCESS (SUCCESS001-SUCCESS099)
Success messages
```csharp
public const string SuccessLogin = "SUCCESS002";
public const string SuccessCreateGroup = "SUCCESS014";
public const string SuccessDeleteGroup = "SUCCESS015";
```

### ANNOUNCEMENT (ANNOUNCEMENT001-ANNOUNCEMENT099)
Announcement-related errors
```csharp
public const string AnnouncementNotFound = "ANNOUNCEMENT001";
```

### SYSTEM (SYS001-SYS099)
System errors
```csharp
public const string UnexpectedError = "SYS001";
```

## Naming Convention:
- Use PascalCase for constant names
- Be descriptive and specific
- Start with category name
- Examples:
  - `GroupNotFound` ? `GROUP001`
  - `ValidationInvalidEmail` ? `VALIDATION001`
  - `SuccessCreateGroup` ? `SUCCESS014`

## Usage in Code:
```csharp
// Throw exception
throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);

// Success message
var message = _messageService.GetMessage(ErrorCodes.SuccessCreateGroup);
return Ok(ApiResponse<T>.Success(ErrorCodes.SuccessCreateGroup, message, data));
```

## Example Usage:
```
Add error code for "User has reached the maximum number of studios allowed" with appropriate messages in both languages.
```

## Translation Guidelines:

### Vietnamese Messages:
- Use formal language
- Be clear and concise
- Provide actionable information
- Examples:
  - "Không t?m th?y nhóm"
  - "B?n không có quy?n truy c?p"
  - "T?o nhóm thành công"

### English Messages:
- Use professional tone
- Be clear and concise
- Provide actionable information
- Examples:
  - "Group not found"
  - "You do not have permission"
  - "Group created successfully"
