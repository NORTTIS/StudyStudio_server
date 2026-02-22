# Add New Repository Method

Add a new method to an existing repository following the project patterns.

## Input Required:
- Entity name
- Method name
- Method purpose
- Parameters
- Return type

## Expected Output:

### 1. Update Repository Interface
File: `Repositories/Interfaces/I{Entity}Repository.cs`

```csharp
public interface I{Entity}Repository
{
    // ...existing methods...
    
    Task<ReturnType> {MethodName}Async(parameters);
}
```

### 2. Implement Repository Method
File: `Repositories/{Entity}Repository.cs`

```csharp
public async Task<ReturnType> {MethodName}Async(parameters)
{
    return await _context.{Entity}
        .Where(/* conditions */)
        .AsNoTracking()  // For read operations
        .ToListAsync();   // Or appropriate method
}
```

## Rules:
1. Always suffix with `Async`
2. Use `AsNoTracking()` for read-only operations
3. Use async methods (`ToListAsync()`, `FirstOrDefaultAsync()`, etc.)
4. Include related entities with `Include()` when needed
5. Add proper filtering conditions
6. Return nullable types when appropriate (`Task<Entity?>`)

## Common Patterns:

### Get By ID:
```csharp
public async Task<Entity?> GetByIdAsync(Guid id)
{
    return await _context.Entities
        .FirstOrDefaultAsync(e => e.Id == id);
}
```

### Get Multiple:
```csharp
public async Task<List<Entity>> GetByIdsAsync(List<Guid> ids)
{
    return await _context.Entities
        .Where(e => ids.Contains(e.Id))
        .AsNoTracking()
        .ToListAsync();
}
```

### Check Existence:
```csharp
public async Task<bool> ExistsAsync(Guid id)
{
    return await _context.Entities
        .AnyAsync(e => e.Id == id);
}
```

### Count:
```csharp
public async Task<int> CountAsync(Expression<Func<Entity, bool>> predicate)
{
    return await _context.Entities
        .Where(predicate)
        .CountAsync();
}
```

### Add:
```csharp
public async Task AddAsync(Entity entity)
{
    _context.Entities.Add(entity);
    await _context.SaveChangesAsync();
}
```

### Update:
```csharp
public async Task UpdateAsync(Entity entity)
{
    _context.Entities.Update(entity);
    await _context.SaveChangesAsync();
}
```

### Soft Delete:
```csharp
public async Task DeleteAsync(Entity entity)
{
    entity.IsActive = false;
    entity.UpdatedAt = DateTime.UtcNow;
    _context.Entities.Update(entity);
    await _context.SaveChangesAsync();
}
```

## Example Usage:
```
Add a method to GroupRepository to get all groups created by a specific user.
```
