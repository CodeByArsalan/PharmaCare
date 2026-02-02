# PharmaCare Coding Standards & Style Guide

## 1. General Naming Conventions
- **Classes/Properties**: PascalCase (e.g., `PartyController`, `AccountName`).
- **Private Fields**: _camelCase (e.g., `_repository`, `_unitOfWork`).
- **Parameters/Variables**: camelCase (e.g., `partyId`, `isValid`).
- **Async Methods**: Suffix with `Async` (e.g., `GetAllAsync`, `CreateAsync`).

## 2. Domain Layer (Entities)
Entities are located in `PharmaCare.Domain.Entities.<Module>`.

- **Base Class**: Inherit from `BaseEntityWithStatus` (or `BaseEntity` if no status tracking needed).
- **Primary Key**: `[Key] public int EntityID { get; set; }`
- **Foreign Keys**:
  - **Property**: Use `_ID` suffix for the integer key (e.g., `AccountHead_ID`). *Note: This is a specific deviation from standard C# conventions.*
  - **Navigation**: Standard PascalCase name (e.g., `AccountHead`).
  - **Attributes**: `[ForeignKey("NavigationPropertyName")]` on the ID property.
- **Validation**: Use DataAnnotations (`[Required]`, `[StringLength]`).
- **Constructor**: Initialize lists/collections if any. Default string properties to `string.Empty`.

**Example:**
```csharp
public class Account : BaseEntityWithStatus
{
    [Key]
    public int AccountID { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    // Foreign Key Pattern
    [ForeignKey("AccountHead")]
    public int? AccountHead_ID { get; set; }
    public AccountHead? AccountHead { get; set; }
}
```

## 3. Application Layer (Services)
Services are located in `PharmaCare.Application.Implementations.<Module>`.

- **Interface**: `IEntityService`.
- **Injection**: Inject `IRepository<Entity>` and `IUnitOfWork`.
- **Return Types**: Return Domain Entities directly (or DTOs if strictly necessary).
- **Structure**:
  - `GetAllAsync()`: Returns `IEnumerable<Entity>`.
  - `GetByIdAsync(int id)`: Returns `Entity?`.
  - `CreateAsync(Entity entity, int userId)`: Sets audit fields (`CreatedBy`, `CreatedAt`) and saves via `_unitOfWork`.
  - `UpdateAsync(Entity entity, int userId)`: Updates specific fields, sets `UpdatedAt/By`, and saves.
  - `ToggleStatusAsync(int id, int userId)`: Toggles `IsActive` instead of hard delete.

**Example:**
```csharp
public class PartyService : IPartyService
{
    private readonly IRepository<Party> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PartyService(IRepository<Party> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Party> CreateAsync(Party party, int userId)
    {
        party.CreatedAt = DateTime.Now;
        party.CreatedBy = userId;
        party.IsActive = true;
        await _repository.AddAsync(party);
        await _unitOfWork.SaveChangesAsync();
        return party;
    }
}
```

## 4. Web Layer (Controllers)
Controllers are located in `PharmaCare.Web.Controllers.<Module>`.

- **Inheritance**: Inherit from `BaseController` (provides `CurrentUserId`, `ShowMessage` helpers).
- **Action Naming**:
  - **Index**: `EntityNameIndex` (e.g., `PartiesIndex`, `AccountsIndex`).
  - **Create**: `AddEntityName` (e.g., `AddParty`).
  - **Edit**: `EditEntityName` (e.g., `EditParty`).
  - **Delete**: `Delete` (Post-only).
- **Feedback**: Use `ShowMessage(MessageType.Success, "Message")` for user feedback.
- **Validation**: Check `ModelState.IsValid`.

**Example:**
```csharp
public class PartyController : BaseController
{
    private readonly IPartyService _partyService;

    // Constructor...

    public async Task<IActionResult> PartiesIndex()
    {
        var list = await _partyService.GetAllAsync();
        return View("PartiesIndex", list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddParty(Party party)
    {
        if (ModelState.IsValid)
        {
            await _partyService.CreateAsync(party, CurrentUserId);
            ShowMessage(MessageType.Success, "Party saved!");
            return RedirectToAction("PartiesIndex");
        }
        return View(party);
    }
}
```

## 5. Web Layer (Views)
Views are located in `Views/<Module>`.

- **Layout**: Use standard Bootstrap 5 grid (`row`, `col-md-*`).
- **Header**: `page-header` div with `page-title` (including `<i>` icon) and "Back" button.
- **Container**: Wrap form content in `card > card-body`.
- **Inputs**: Use `asp-for`, `form-control`/`form-select`.
- **Validation**: `asp-validation-for` spans with `text-danger`.
- **Scripts**: Dictionary-based dropdowns fetch data via AJAX in `@section Scripts`.

**Example:**
```html
@model Party
@{ ViewData["Title"] = "Add Party"; }

<div class="page-header">
    <h4 class="page-title"><i class="fas fa-plus"></i> Add Party</h4>
    <a asp-action="PartiesIndex" class="btn btn-outline-secondary">Back</a>
</div>

<div class="card">
    <div class="card-body">
        <form asp-action="AddParty" method="post">
            <div class="row g-3">
                <div class="col-md-6">
                    <label class="form-label">Name</label>
                    <input asp-for="Name" class="form-control" required />
                    <span asp-validation-for="Name" class="text-danger small"></span>
                </div>
            </div>
            <div class="mt-4 text-end">
                <button type="submit" class="btn btn-primary">Save</button>
            </div>
        </form>
    </div>
</div>
```
