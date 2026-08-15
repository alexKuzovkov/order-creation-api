# Updating an existing checkout

If you copy the refactored files over the previous repository version, delete the legacy aggregate infrastructure file before building:

```powershell
.\cleanup_legacy.ps1
```

or:

```bash
./cleanup_legacy.sh
```

The old file

```text
src/OrderCreation.Api/Infrastructure/Infrastructure.cs
```

contained `AuthorizationPolicies`, `ICurrentUser`, `HttpCurrentUser`, `InMemoryOrderRepository`, and `GlobalExceptionHandler`. In the refactored version these types live in separate files. Leaving the aggregate file in an existing checkout produces `CS0101`, `CS0229`, `CS8863`, and `CS0111` duplicate-definition errors.

Then run:

```powershell
git add -A
dotnet build OrderCreationSample.sln -c Release
```
