using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PharmaCare.Domain.ViewModels;
using PharmaCare.Web.Utilities;
using System.Reflection;

namespace PharmaCare.Web.Controllers;

public class BaseController : Controller, IAsyncActionFilter
{
    protected int DecryptId(string encryptedId)
    {
        if (string.IsNullOrEmpty(encryptedId)) return 0;

        // If it's already a number, return it
        if (int.TryParse(encryptedId, out int rawId)) return rawId;

        // Otherwise try to decrypt
        string decrypted = Utility.DecryptURL(encryptedId);
        return int.TryParse(decrypted, out int result) ? result : 0;
    }

    public int LoginUserID { get; private set; }
    public string LoginUserName { get; private set; }
    public string LoginUserEmail { get; private set; }
    public int LoginUserTypeID { get; set; }
    public string LoginUserType { get; set; }
    public int? LoginUserStoreID { get; private set; }
    public string? LoginUserStoreName { get; set; }
    public string RequestType { get; set; }
    public string ActionMethod { get; set; }
    public string ControllerName { get; set; }
    public List<string> UserAllowedUrls { get; private set; } = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {

        if (!User.Identity.IsAuthenticated)
        {
            context.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Account", action = "Login" }));
            return;
        }

        var loginUserClaim = User.Claims.FirstOrDefault(d => d.Type == "LoginUserDetail")?.Value;
        if (string.IsNullOrEmpty(loginUserClaim))
        {
            // Claim not found - redirect to login
            context.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Account", action = "Login" }));
            return;
        }

        var loginUserDetail = JsonConvert.DeserializeObject<LoginUserViewModel>(loginUserClaim);
        if (loginUserDetail == null)
        {
            context.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Account", action = "Login" }));
            return;
        }

        var routeData = context.HttpContext.GetRouteData();
        var controllerName = routeData.Values["controller"]?.ToString();
        var actionName = routeData.Values["action"]?.ToString();

        var settings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            // skip below properties in request.
            ContractResolver = new ExcludePropertiesContractResolver(new[] { "CreatedBy", "CreatedDate", "UpdatedBy", "UpdatedDate" }),
        };

        LoginUserID = loginUserDetail.UserID;
        LoginUserName = loginUserDetail.UserName;
        LoginUserEmail = loginUserDetail.Email;
        LoginUserTypeID = loginUserDetail.LoginUserTypeID;
        LoginUserType = loginUserDetail.LoginUserType;
        LoginUserStoreID = loginUserDetail.Store_ID;
        TempData["UserType"] = LoginUserType;
        //ViewData["userId"] = LoginUserID;
        HttpContext.Session.SetInt32("UserId", LoginUserID);
        ViewBag.LoginUserID = LoginUserID;
        ViewBag.IsAdmin = loginUserDetail.LoginUserTypeID == 1;

        // ** Initialize StoreContext **
        var storeContext = context.HttpContext.RequestServices.GetService<PharmaCare.Domain.Interfaces.IStoreContext>();
        if (storeContext != null)
        {
            // UserTypeID 1 is Super Admin
            bool isAdmin = LoginUserTypeID == 1;
            storeContext.SetContext(LoginUserStoreID, isAdmin);
        }

        RequestType = context.HttpContext.Request.Method.ToString2();
        ControllerName = controllerName;
        ActionMethod = actionName;

        // **URL-Based Authorization Check**
        // Get user's assigned pages from session
        var userPagesJson = HttpContext.Session.GetString("AspNetUserPage");
        if (!string.IsNullOrEmpty(userPagesJson))
        {
            try
            {
                var menuItems = System.Text.Json.JsonSerializer.Deserialize<List<MenuItemDto>>(userPagesJson);
                if (menuItems != null)
                {
                    // Build list of allowed URLs
                    UserAllowedUrls = new List<string>();
                    foreach (var parent in menuItems)
                    {
                        // Add parent URLs
                        if (!string.IsNullOrEmpty(parent.ControllerName))
                        {
                            UserAllowedUrls.Add($"/{parent.ControllerName}".ToLower());
                            if (!string.IsNullOrEmpty(parent.ViewName))
                            {
                                UserAllowedUrls.Add($"/{parent.ControllerName}/{parent.ViewName}".ToLower());
                            }
                        }
                        UserAllowedUrls.AddRange(parent.Urls.Select(u => u.ToLower()));

                        // Add child URLs
                        if (parent.Children != null)
                        {
                            foreach (var child in parent.Children)
                            {
                                if (!string.IsNullOrEmpty(child.ControllerName))
                                {
                                    UserAllowedUrls.Add($"/{child.ControllerName}".ToLower());
                                    if (!string.IsNullOrEmpty(child.ViewName))
                                    {
                                        UserAllowedUrls.Add($"/{child.ControllerName}/{child.ViewName}".ToLower());
                                    }
                                }
                                UserAllowedUrls.AddRange(child.Urls.Select(u => u.ToLower()));
                            }
                        }
                    }
                }
            }
            catch
            {
                // Failed to deserialize - continue without URL checking
            }
        }

        // Check if current URL is allowed
        var currentUrl = $"/{controllerName}/{actionName}".ToLower();
        var currentControllerUrl = $"/{controllerName}".ToLower();

        // List of always-allowed pages (dashboard, error, logout)
        var alwaysAllowedUrls = new List<string>
        {
            "/Account/Login",
            "/Account/Logout",
            "/account/forgotpassword",
            "/account/resetpassword",
            "/account/accessdenied",
            "/usertype/usertypes",
            "/usertype/editusertypeinline",
            "/usertype/updateusertype",
            "/usertype/createusertype",
            "/home/index",
            "/home/error",
            "/party/getpartiesbytype",
            "/subhead/getsubheadsbyhead",
            "/chartofaccount/getaccounts",
            "/accountmapping/getpartiesbytype",
            "/accountmapping/getsubheadsbyhead",
            "/purchasereturn/processrefund",
            "/supplierpayment/cancelsupplierpayment",
            "/supplierpayment/getgrndetails",
            "/supplierpayment/getsuppliergrns",
            "/category/getsubcategories",
            "/product/generatebarcode",
            "/pos/searchproducts",
            "/pos/getcustomers",
            "/pos/addtocart",
            "/pos/getcartdata",
            "/pos/updatecartitem",
            "/pos/removefromcart",
            "/pos/clearcart",
            "/pos/checkout",
            "/pos/receipt",
            "/pos/newsale",
            "/quotation/converttosale",
            "/quotation/searchproducts",
            "/quotation/getbatchdetails",
            "/heldsale/holdsale",
            "/heldsale/resumesale",
            "/heldsale/getheldsalescount",
            "/customerpayment/getoutstandingsales",
            "/customerpayment/getpaymenthistory",
            "/customerpayment/createpaymentajax",
            "/purchasereturn/getbatchesforsupplier",
            "/purchasereturn/approve",
            "/stockalerts/generatealerts",
            "/stockalerts/resolvealert",
            "/stockalerts/getalertcount",
            "/stocktake/updateitem",
            "/stocktake/complete",
            "/stocktransfer/approvetransfer",
            "/stocktransfer/getbatchesforstore",
            "/stocktransfer/approvetransfer",
            "/stocktransfer/getbatchesforStore",
            "/stockadjustment/searchbatches",
            "/cash/recordcashtransaction",
            "/journalentry/post",
            "/journalentry/void",
            "/fiscalperiod/fiscalperiodindex",
            "/fiscalperiod/periods",
            "/fiscalperiod/closeperiod",
            "/fiscalperiod/reopenperiod",
            "/fiscalperiod/lockperiod",
            "/fiscalperiod/createfiscalyear",
            "/fiscalperiod/closefiscalyear"
        };

        // Check authorization - only block access if:
        // 1. Page is NOT in always-allowed list AND
        // 2. User has pages assigned (UserAllowedUrls is not empty) AND
        // 3. Page is NOT in user's allowed pages
        if (!alwaysAllowedUrls.Contains(currentUrl))
        {
            // User is trying to access a restricted page
            if (UserAllowedUrls.Any())
            {
                // User has pages assigned - check if this page is allowed
                if (!UserAllowedUrls.Contains(currentUrl))
                {
                    // User doesn't have access - redirect to dashboard
                    TempData["Message"] = "You don't have permission to access this page.";
                    TempData["Type"] = "error";
                    TempData["Icon"] = "bx bx-error";
                    context.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Home", action = "Index" }));
                    return;
                }
            }
            else
            {
                // User has NO pages assigned at all - only allow Home
                if (!currentControllerUrl.Equals("/home"))
                {
                    TempData["Message"] = "No pages have been assigned to your account. Please contact administrator.";
                    TempData["Type"] = "warning";
                    TempData["Icon"] = "bx bx-error-circle";
                    context.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Home", action = "Index" }));
                    return;
                }
            }
        }

        await next();
    }

    public void ShowMessage(MessageBox MessageType, string Message = "Record Saved Successfully")
    {
        TempData["Message"] = Message;
        switch (MessageType)
        {
            case MessageBox.Success:
                TempData["Icon"] = "bx bx-check-circle";
                TempData["Type"] = "success";
                break;
            case MessageBox.Error:
                TempData["Icon"] = "bx bx-x-circle";
                TempData["Type"] = "error";
                break;
            case MessageBox.Warning:
                TempData["Icon"] = "bx bx-error";
                TempData["Type"] = "warning";
                break;
            case MessageBox.Info:
                TempData["Icon"] = "bx bx-info-circle";
                TempData["Type"] = "info";
                break;
            default:
                TempData["Icon"] = "";
                TempData["Type"] = "";
                break;
        }
    }

}
public class ExcludePropertiesContractResolver : DefaultContractResolver
{
    private readonly HashSet<string> excludedProperties;

    public ExcludePropertiesContractResolver(IEnumerable<string> excludedProperties)
    {
        this.excludedProperties = new HashSet<string>(excludedProperties, StringComparer.OrdinalIgnoreCase);
    }

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);

        // Exclude properties based on the provided list
        if (excludedProperties.Contains(property.PropertyName))
        {
            property.ShouldSerialize = _ => false;
        }

        return property;
    }
}

public enum MessageTitle
{
    Information = 0,
    Saved = 1,
    Updated = 2,
    Error = 3,
    Warning = 4,
    Deleted = 5
}
public enum MessageBox
{
    Success = 1,
    Error = 2,
    Warning = 3,
    Info = 4
}
