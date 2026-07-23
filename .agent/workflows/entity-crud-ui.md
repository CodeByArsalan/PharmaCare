---
description: Standard UI pattern for CRUD entity pages (Index, Add, Edit views)
---

# PharmaCare Entity Page UI Pattern

This skill defines the standard UI pattern for all CRUD entity pages in PharmaCare. Use this pattern when creating or updating Index, Add, and Edit views for entities. Reference implementation: `Views/Category/CategoriesIndex.cshtml` + `Controllers/Configuration/CategoryController.cs`.

## Page Structure Overview

All entity pages follow this consistent layout:
- **Index Page**: Title on left, Add button on right, DataTables-powered table with SweetAlert status-toggle confirmation
- **Add Page**: Title on left, Back to List on right, col-md-6 form controls, buttons in right corner
- **Edit Page**: Same as Add Page (hidden audit fields; IsActive kept via hidden input)

Success/error feedback is NOT rendered in the view. Controllers call the `BaseController.ShowMessage(...)` helper, and the shared `_ToastNotifications.cshtml` partial (included in `_Layout.cshtml`) renders the toast automatically. Never add `TempData["Success"]` alert blocks to views.

---

## Index Page Template (`{Entities}Index.cshtml`)

```html
@using PharmaCare.Web.Utilities
@model IEnumerable<PharmaCare.Domain.Entities.NAMESPACE.ENTITY>
@{
    ViewData["Title"] = "Entity Management";
}

<!-- Page Header -->
<div class="page-header">
    <h4 class="page-title">
        <i class="fas fa-ICON"></i>
        Entity Management
    </h4>
    <a asp-action="AddEntity" class="btn btn-primary">
        <i class="fas fa-plus me-2"></i>Add Entity
    </a>
</div>

<div class="card">
    <div class="card-body">
        <div class="table-responsive">
        <table class="table table-hover datatable">
            <thead>
                <tr>
                    <th scope="col" style="width: 60px;">S.No</th>
                    <th scope="col">Column 1</th>
                    <th scope="col">Column 2</th>
                    <th scope="col">Status</th>
                    <th scope="col" class="text-end">Actions</th>
                </tr>
            </thead>
            <tbody>
                @{ int count = 1; }
                @foreach (var item in Model)
                {
                    <tr>
                        <td>@(count++)</td>
                        <td><strong>@item.Name</strong></td>
                        <td>@item.OtherProperty</td>
                        <td>
                            @if (item.IsActive)
                            {
                                <span class="badge bg-success">Active</span>
                            }
                            else
                            {
                                <span class="badge bg-danger">Inactive</span>
                            }
                        </td>
                        <td class="text-end">
                            <a asp-action="EditEntity" asp-route-id="@item.EntityID.EncryptId()" class="btn btn-sm btn-outline-primary" title="Edit entity" aria-label="Edit entity">
                                <i class="fas fa-edit"></i>
                            </a>
                            <button type="button" class="btn btn-sm btn-outline-warning btn-toggle"
                                    data-id="@item.EntityID.EncryptId()" data-name="@item.Name" data-status="@(item.IsActive ? "deactivate" : "activate")" title="Toggle entity status" aria-label="Toggle entity status">
                                <i class="fas fa-power-off"></i>
                            </button>
                        </td>
                    </tr>
                }
            </tbody>
        </table>
        </div>
    </div>
</div>

<!-- Hidden form for status toggle POST -->
<form id="deleteForm" method="post" style="display: none;">
    @Html.AntiForgeryToken()
</form>

@section Scripts {
    <script>
        document.querySelectorAll('.btn-toggle').forEach(function(btn) {
            btn.addEventListener('click', function() {
                const id = this.getAttribute('data-id');
                const name = this.getAttribute('data-name');
                const status = this.getAttribute('data-status');
                const isDeactivate = status === 'deactivate';

                Swal.fire({
                    title: isDeactivate ? 'Deactivate Entity?' : 'Activate Entity?',
                    text: `Are you sure you want to ${status} "${name}"?`,
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonColor: isDeactivate ? '#f59e0b' : '#10b981',
                    cancelButtonColor: '#64748b',
                    confirmButtonText: isDeactivate ? 'Yes, deactivate it!' : 'Yes, activate it!',
                    cancelButtonText: 'Cancel',
                    background: 'var(--bg-card)',
                    color: 'var(--text-primary)'
                }).then((result) => {
                    if (result.isConfirmed) {
                        const form = document.getElementById('deleteForm');
                        form.action = '/Controller/Delete/' + id;
                        form.submit();
                    }
                });
            });
        });
    </script>
}
```

Notes:
- `class="table table-hover datatable"` — the `datatable` class triggers automatic DataTables initialization (search, paging, sorting). Do NOT add a manual empty-state row; DataTables renders its own empty message.
- Route ids are ALWAYS encrypted with the `EncryptId()` extension (`@using PharmaCare.Web.Utilities`); controllers decrypt with `Utility.DecryptId(id)`.
- There is no hard delete. The "Delete" action toggles `IsActive` — hence the `.btn-toggle` button with `data-status`.

---

## Add Page Template (`Add{Entity}.cshtml`)

```html
@model PharmaCare.Domain.Entities.NAMESPACE.ENTITY
@{
    ViewData["Title"] = "Add New Entity";
}

<!-- Page Header -->
<div class="page-header">
    <h4 class="page-title">
        <i class="fas fa-plus-circle"></i>
        Add New Entity
    </h4>
    <a asp-action="EntitiesIndex" class="btn btn-outline-secondary">
        <i class="fas fa-arrow-left me-2"></i>Back to List
    </a>
</div>

<div class="card">
    <div class="card-body">
        <form asp-action="AddEntity" method="post">
            <div class="row g-3">
                <div class="col-md-6">
                    <label class="form-label">Field 1 <span class="text-danger">*</span></label>
                    <input asp-for="Field1" class="form-control" placeholder="Enter value" required />
                    <span asp-validation-for="Field1" class="text-danger small"></span>
                </div>
                
                <div class="col-md-6">
                    <label class="form-label">Field 2</label>
                    <input asp-for="Field2" class="form-control" placeholder="Enter value" />
                </div>
                
                <!-- For dropdowns (combobox repository can be injected with @inject) -->
                <div class="col-md-6">
                    <label class="form-label">Related Entity</label>
                    <select asp-for="RelatedEntity_ID" asp-items="ViewBag.RelatedEntities" class="form-select">
                        <option value="">-- Select Option --</option>
                    </select>
                </div>
            </div>
            
            <div class="text-end mt-4">
                <a asp-action="EntitiesIndex" class="btn btn-outline-secondary me-2">Cancel</a>
                <button type="submit" class="btn btn-primary">
                    <i class="fas fa-save me-2"></i>Save Entity
                </button>
            </div>
        </form>
    </div>
</div>
```

---

## Edit Page Template (`Edit{Entity}.cshtml`)

```html
@model PharmaCare.Domain.Entities.NAMESPACE.ENTITY
@{
    ViewData["Title"] = "Edit Entity";
}

<!-- Page Header -->
<div class="page-header">
    <h4 class="page-title">
        <i class="fas fa-edit"></i>
        Edit Entity
    </h4>
    <a asp-action="EntitiesIndex" class="btn btn-outline-secondary">
        <i class="fas fa-arrow-left me-2"></i>Back to List
    </a>
</div>

<div class="card">
    <div class="card-body">
        <form asp-action="EditEntity" method="post">
            <input type="hidden" asp-for="EntityID" />
            <input type="hidden" asp-for="CreatedAt" />
            <input type="hidden" asp-for="CreatedBy" />
            <input type="hidden" asp-for="IsActive" value="true" />
            
            <div class="row g-3">
                <div class="col-md-6">
                    <label class="form-label">Field 1 <span class="text-danger">*</span></label>
                    <input asp-for="Field1" class="form-control" placeholder="Enter value" required />
                    <span asp-validation-for="Field1" class="text-danger small"></span>
                </div>
                
                <div class="col-md-6">
                    <label class="form-label">Field 2</label>
                    <input asp-for="Field2" class="form-control" placeholder="Enter value" />
                </div>
            </div>
            
            <div class="text-end mt-4">
                <a asp-action="EntitiesIndex" class="btn btn-outline-secondary me-2">Cancel</a>
                <button type="submit" class="btn btn-primary">
                    <i class="fas fa-save me-2"></i>Update Entity
                </button>
            </div>
        </form>
    </div>
</div>
```

---

## Controller Pattern

Controllers inherit `BaseController` and follow this shape (see `CategoryController` for the reference):

```csharp
public async Task<IActionResult> EntitiesIndex()
{
    var items = await _entityService.GetAllAsync();
    return View("EntitiesIndex", items);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AddEntity(
    [Bind("Name,Field1,Field2,Related_ID")] Entity entity)
{
    if (ModelState.IsValid)
    {
        try
        {
            await _entityService.CreateAsync(entity, CurrentUserId);
            ShowMessage(MessageType.Success, "Entity created successfully!");
            return RedirectToAction("EntitiesIndex");
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(MessageType.Error, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating entity");
            ShowMessage(MessageType.Error, "An unexpected error occurred while saving the entity.");
        }
    }
    return View(entity);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Delete(string id)
{
    int entityId = Utility.DecryptId(id);
    if (entityId == 0) return NotFound();

    try
    {
        await _entityService.ToggleStatusAsync(entityId, CurrentUserId);
        ShowMessage(MessageType.Success, "Entity status updated successfully!");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error toggling entity {EntityId}", entityId);
        ShowMessage(MessageType.Error, "Could not change the entity's status. Please try again.");
    }
    return RedirectToAction("EntitiesIndex");
}
```

Rules:
- Every POST action carries `[ValidateAntiForgeryToken]` and a `[Bind("...")]` allowlist of exactly the fields the form posts.
- Add/Edit POST bodies wrap service calls in try/catch: `InvalidOperationException` → `ShowMessage(MessageType.Error, ex.Message)` (business-rule message shown to user); generic `Exception` → `_logger.LogError(...)` + a generic `ShowMessage` error.
- GET Edit takes the encrypted `string id`, decrypts with `Utility.DecryptId(id)`, and returns `NotFound()` on 0/mismatch.
- Feedback goes through `ShowMessage(MessageType.Success|Error|Warning|Info, "...")` — never write to `TempData` directly.

---

## Key UI Rules

### Page Header
- Use `<div class="page-header">` wrapper
- Title on **left** with icon: `<h4 class="page-title"><i class="fas fa-ICON"></i> Title</h4>`
- Action button on **right**

### Index Table
- `<table class="table table-hover datatable">` inside `<div class="table-responsive">` inside `card-body`
- The `datatable` class auto-initializes DataTables — no manual empty-state markup, no `mb-0`/`p-0` tricks
- First column is an `S.No` counter (`@{ int count = 1; }` … `@(count++)`)

### Form Layout
- Use `<div class="row g-3">` for form grid
- Form controls use `col-md-6` (2 per row on medium+ screens)
- Required fields have `<span class="text-danger">*</span>`

### Button Placement
- Use `<div class="text-end mt-4">` for button container
- Cancel button first with `me-2` margin
- Primary action button last

### Status Toggle Confirmation (the "Delete" action)
- No hard deletes — the `Delete` POST action toggles `IsActive` via the service's `ToggleStatusAsync`
- Button uses `btn-outline-warning btn-toggle` with `data-id` (encrypted), `data-name`, `data-status` attributes and a `fa-power-off` icon
- SweetAlert2 confirm MUST use theme variables so it follows the light/dark toggle: `background: 'var(--bg-card)'`, `color: 'var(--text-primary)'` — never hardcoded hex backgrounds. Accent hex values for `confirmButtonColor`/`cancelButtonColor` are fine.
- Hidden form (`#deleteForm`) with `@Html.AntiForgeryToken()` for the POST submission

### Icons (Font Awesome)
- Store: `fa-store`
- Category: `fa-tags`
- SubCategory: `fa-layer-group`
- Product: `fa-pills`
- Party: `fa-users`
- Add: `fa-plus`, `fa-plus-circle`
- Edit: `fa-edit`
- Toggle status: `fa-power-off`
- Save: `fa-save`
- Back: `fa-arrow-left`

---

## Controller Action Naming Convention

| Action | Route |
|--------|-------|
| Index  | `{Entities}Index` |
| Add    | `Add{Entity}` |
| Edit   | `Edit{Entity}` |
| Delete (toggles IsActive) | `Delete` (POST, encrypted id) |
