# Refactor to Repository Pattern

Refactor code that directly accesses DbContext to follow the Repository pattern.

## Problem:
Service classes are directly accessing `DbContext` instead of using repositories.

? **Anti-Pattern:**
```csharp
public class MyService
{
    private readonly StudioDbContext _db;
    
    public async Task DoSomething()
    {
        var data = await _db.MyTable
            .Where(x => x.IsActive)
            .ToListAsync();
    }
}
```

## Solution Steps:

### 1. Identify Direct DbContext Access
Look for patterns like:
```csharp
_db.{Table}.{Query}
await _db.{Table}.ToListAsync()
```

### 2. Create Repository Interface
File: `Repositories/Interfaces/I{Entity}Repository.cs`

```csharp
public interface I{Entity}Repository
{
    Task<List<Entity>> Get{Description}Async(parameters);
}
```

### 3. Implement Repository
File: `Repositories/{Entity}Repository.cs`

```csharp
public class {Entity}Repository : I{Entity}Repository
{
    private readonly StudioDbContext _context;
    
    public {Entity}Repository(StudioDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<Entity>> Get{Description}Async(parameters)
    {
        return await _context.{Table}
            .Where(/* conditions */)
            .AsNoTracking()
            .ToListAsync();
    }
}
```

### 4. Register Repository in DI
File: `Program.cs`

```csharp
builder.Services.AddScoped<I{Entity}Repository, {Entity}Repository>();
```

### 5. Update Service
File: `Services/{Service}.cs`

**Before:**
```csharp
public class MyService
{
    private readonly StudioDbContext _db;
    
    public MyService(StudioDbContext db)
    {
        _db = db;
    }
    
    public async Task DoSomething()
    {
        var data = await _db.MyTable.ToListAsync();
    }
}
```

**After:**
```csharp
public class MyService
{
    private readonly I{Entity}Repository _repository;
    
    public MyService(I{Entity}Repository repository)
    {
        _repository = repository;
    }
    
    public async Task DoSomething()
    {
        var data = await _repository.GetAllAsync();
    }
}
```

### 6. Remove DbContext Dependency
- Remove `StudioDbContext _db` field
- Remove from constructor
- Replace all `_db.{Table}` calls with repository methods

## Common Refactoring Patterns:

### Get All:
```csharp
// Before
var items = await _db.Items.ToListAsync();

// After
var items = await _itemRepository.GetAllAsync();
```

### Get By ID:
```csharp
// Before
var item = await _db.Items.FirstOrDefaultAsync(x => x.Id == id);

// After
var item = await _itemRepository.GetByIdAsync(id);
```

### Get with Filter:
```csharp
// Before
var items = await _db.Items
    .Where(x => x.UserId == userId && x.IsActive)
    .ToListAsync();

// After
var items = await _itemRepository.GetByUserIdAsync(userId);
```

### Count:
```csharp
// Before
var count = await _db.Items.CountAsync(x => x.UserId == userId);

// After
var count = await _itemRepository.CountByUserAsync(userId);
```

### Check Existence:
```csharp
// Before
var exists = await _db.Items.AnyAsync(x => x.Name == name);

// After
var exists = await _itemRepository.ExistsByNameAsync(name);
```

## Checklist:
- [ ] Create repository interface
- [ ] Implement repository
- [ ] Register in DI container
- [ ] Update service constructor
- [ ] Replace all DbContext calls
- [ ] Remove DbContext dependency
- [ ] Test the refactored code

## Example Usage:
```
Refactor GroupService.GetGroupsAsync() method to use repositories instead of direct DbContext access for Favourites, Users, Studios, GroupParticipants, and Tasks.
```

## Architecture Diagram:
```
? BEFORE:
Controller ? Service ? DbContext ? Database

? AFTER:
Controller ? Service ? Repository ? DbContext ? Database
```
