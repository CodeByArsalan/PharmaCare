namespace PharmaCare.Web.Filters;

/// <summary>
/// Links an action method to a specific page's permissions.
/// When applied, the authorization filter will check if the user has access to the
/// specified Controller/Action page instead of the current action's controller/action.
/// 
/// Use this for AJAX endpoints, sub-actions, or any action that should inherit
/// permissions from a parent page without requiring a PageUrl database entry.
/// 
/// Example usage:
///   [LinkedToPage("Category", "CategoriesIndex")]
///   public async Task&lt;IActionResult&gt; GetSubCategories(int categoryId) { ... }
/// 
///   [LinkedToPage("Product", "ProductsIndex")]
///   public async Task&lt;IActionResult&gt; GetSubCategoriesByCategoryId(int categoryId) { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class LinkedToPageAttribute : Attribute
{
    /// <summary>
    /// The controller name of the parent page to inherit permissions from.
    /// </summary>
    public string Controller { get; }

    /// <summary>
    /// The action name of the parent page to inherit permissions from.
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// The permission type required (view, create, edit, delete).
    /// Defaults to "view". Override this when the action requires a specific permission.
    /// </summary>
    public string PermissionType { get; set; } = "view";

    /// <summary>
    /// Creates a new LinkedToPageAttribute.
    /// </summary>
    /// <param name="controller">Controller name of the parent page (e.g., "Category")</param>
    /// <param name="action">Action name of the parent page (e.g., "CategoriesIndex")</param>
    public LinkedToPageAttribute(string controller, string action)
    {
        Controller = controller;
        Action = action;
    }
}
