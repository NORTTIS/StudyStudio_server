# Create New API Endpoint

Generate a complete API endpoint following the project's layered architecture pattern.

## Input Required:
- Entity name (e.g., "Group", "Task", "User")
- Action (e.g., "Create", "Update", "Delete", "Get")
- Request parameters
- Response structure

## Expected Output:

### 1. Create Request DTO
File: `Models/DTOs/Request/{Action}{Entity}Request.cs`
- Include DataAnnotations validation
- Use error codes from `ErrorCodes.cs`
- Set appropriate default values

### 2. Create Response DTO
File: `Models/DTOs/Response/{Action}{Entity}Response.cs`
- Include all necessary fields
- Use appropriate data types

### 3. Update Error Codes
File: `Exceptions/ErrorCodes.cs`
- Add new error codes if needed
- Follow naming convention: `{CATEGORY}###`

### 4. Update Localization
Files: `Resources/Errors/errors.vi.json` and `errors.en.json`
- Add messages for all new error codes
- Provide both Vietnamese and English translations

### 5. Create/Update Repository Interface
File: `Repositories/Interfaces/I{Entity}Repository.cs`
- Add method signature: `Task<T> {Action}{Entity}Async(...)`

### 6. Implement Repository
File: `Repositories/{Entity}Repository.cs`
- Implement the method
- Use `AsNoTracking()` for read operations
- Use async methods
- Follow soft delete pattern if applicable

### 7. Update Service Interface
File: `Services/Interfaces/I{Entity}Service.cs`
- Add method signature

### 8. Implement Service
File: `Services/{Entity}Service.cs`
- Implement business logic
- Call repositories only (NO direct DbContext access)
- Throw `AppException` for errors
- Validate business rules

### 9. Create/Update Controller
File: `Controllers/{Entity}Controller.cs`
- Add endpoint with proper HTTP verb attribute
- Extract userId from JWT claims
- Check admin status (if required)
- Call service method
- Return `ApiResponse<T>`

## Architecture Rules:
```
Controller ? Service ? Repository ? DbContext ? Database
```

## Note:
API documentation should be created manually after the endpoint is implemented and tested.

## Example Usage:
```
Create an API endpoint to update a Group's name and description.
Only the group Owner should be able to update it.
