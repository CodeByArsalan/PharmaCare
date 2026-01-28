---
description: Standard UI pattern for CRUD entity pages (Index, Add, Edit views)
---

# PharmaCare Entity Page UI Pattern

This skill defines the standard UI pattern for all CRUD entity pages in PharmaCare. Use this pattern when creating or updating Index, Add, and Edit views for entities.

## Page Structure Overview

All entity pages follow this consistent layout:
- **Index Page**: Title on left, Add button on right, table with SweetAlert delete
- **Add Page**: Title on left, Back to List on right, col-md-6 form controls, buttons in right corner
- **Edit Page**: Same as Add Page (without IsActive toggle unless explicitly needed)

---

## Index Page Template (`{Entities}Index.cshtml`)

```html
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

@if (TempData["Success"] != null)
{
    <div class="alert alert-success alert-dismissible fade show" role="alert">
        <i class="fas fa-check-circle me-2"></i>@TempData["Success"]
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    </div>
}

<div class="card">
    <div class="card-body p-0">
        <table class="table mb-0">
            <thead>
                <tr>
                    <th>Column 1</th>
                    <th>Column 2</th>
                    <th>Status</th>
                    <th class="text-end">Actions</th>
                </tr>
            </thead>
            <tbody>
                @if (Model.Any())
                {
                    @foreach (var item in Model)
                    {
                        <tr>
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
                                <a asp-action="EditEntity" asp-route-id="@item.EntityID" class="btn btn-sm btn-outline-primary">
                                    <i class="fas fa-edit"></i>
                                </a>
                                <button type="button" class="btn btn-sm btn-outline-danger btn-delete" 
                                        data-id="@item.EntityID" data-name="@item.Name">
                                    <i class="fas fa-trash"></i>
                                </button>
                            </td>
                        </tr>
                    }
                }
                else
                {
                    <tr>
                        <td colspan="4" class="text-center py-4">
                            <div class="empty-state">
                                <i class="fas fa-ICON"></i>
                                <p class="mb-0">No entities found. Click "Add Entity" to create one.</p>
                            </div>
                        </td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
</div>

<!-- Hidden form for delete -->
<form id="deleteForm" method="post" style="display: none;">
    @Html.AntiForgeryToken()
</form>

@section Scripts {
    <script>
        document.querySelectorAll('.btn-delete').forEach(function(btn) {
            btn.addEventListener('click', function() {
                const id = this.getAttribute('data-id');
                const name = this.getAttribute('data-name');
                
                Swal.fire({
                    title: 'Delete Entity?',
                    text: `Are you sure you want to delete "${name}"?`,
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonColor: '#dc2626',
                    cancelButtonColor: '#64748b',
                    confirmButtonText: 'Yes, delete it!',
                    cancelButtonText: 'Cancel',
                    background: '#1e293b',
                    color: '#f1f5f9'
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
                
                <!-- For dropdowns -->
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

## Key UI Rules

### Page Header
- Use `<div class="page-header">` wrapper
- Title on **left** with icon: `<h4 class="page-title"><i class="fas fa-ICON"></i> Title</h4>`
- Action button on **right**

### Form Layout
- Use `<div class="row g-3">` for form grid
- Form controls use `col-md-6` (2 per row on medium+ screens)
- Required fields have `<span class="text-danger">*</span>`

### Button Placement
- Use `<div class="text-end mt-4">` for button container
- Cancel button first with `me-2` margin
- Primary action button last

### Delete Confirmation
- Use SweetAlert2 with dark theme (`background: '#1e293b'`, `color: '#f1f5f9'`)
- Hidden form for POST submission
- Button with `btn-delete` class and `data-id`, `data-name` attributes

### Icons (Font Awesome)
- Store: `fa-store`
- Category: `fa-tags`
- SubCategory: `fa-layer-group`
- Product: `fa-pills`
- Party: `fa-users`
- Add: `fa-plus`, `fa-plus-circle`
- Edit: `fa-edit`
- Delete: `fa-trash`
- Save: `fa-save`
- Back: `fa-arrow-left`

---

## Controller Action Naming Convention

| Action | Route |
|--------|-------|
| Index  | `{Entities}Index` |
| Add    | `Add{Entity}` |
| Edit   | `Edit{Entity}` |
| Delete | `Delete` |
