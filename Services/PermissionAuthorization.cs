using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using BeautyShop.Models;

namespace BeautyShop.Services
{
    /// <summary>
    /// Requirement cho Policy-based Authorization theo từng mã quyền chức năng (Bước 6).
    /// </summary>
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string PermissionCode { get; }

        public PermissionRequirement(string permissionCode)
        {
            PermissionCode = permissionCode;
        }
    }

    /// <summary>
    /// Handler kiểm tra quyền của người dùng thông qua ICurrentUserScope.
    /// Đọc quyền theo vai trò hiện hành từ CSDL nên mọi thay đổi vai trò/quyền của Admin có hiệu lực tức thì.
    /// </summary>
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ICurrentUserScope _userScope;

        public PermissionAuthorizationHandler(ICurrentUserScope userScope)
        {
            _userScope = userScope;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            if (!_userScope.IsAuthenticated)
            {
                return;
            }

            if (await _userScope.HasPermissionAsync(requirement.PermissionCode))
            {
                context.Succeed(requirement);
            }
        }
    }

    /// <summary>
    /// Attribute phân quyền dựa trên Policy ([RequirePermission(AppPermissions.ProductManage)])
    /// Kế thừa AuthorizeAttribute(Policy = permissionCode) đồng thời thực thi IAsyncAuthorizationFilter
    /// để trả về đúng mã trạng thái HTTP 403 Forbidden kèm trang thông báo tiếng Việt nêu rõ quyền còn thiếu.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class RequirePermissionAttribute : AuthorizeAttribute, IAsyncAuthorizationFilter
    {
        public string PermissionCode { get; }

        public RequirePermissionAttribute(string permissionCode) : base(policy: permissionCode)
        {
            PermissionCode = permissionCode;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var scope = context.HttpContext.RequestServices.GetService<ICurrentUserScope>();
            if (scope == null)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "" });
                return;
            }

            // 1. Chưa đăng nhập hoặc tài khoản bị khóa -> Chuyển về trang đăng nhập
            if (!scope.IsAuthenticated)
            {
                context.Result = new RedirectToActionResult("Login", "Account", new { area = "", returnUrl = context.HttpContext.Request.Path });
                return;
            }

            // 2. Kiểm tra quyền thông qua ICurrentUserScope (đồng bộ với Policy Handler)
            bool hasPermission = await scope.HasPermissionAsync(PermissionCode);
            if (!hasPermission)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;

                var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), context.ModelState)
                {
                    ["RequiredPermission"] = PermissionCode,
                    ["RequiredPermissionName"] = AppPermissions.GetVietnameseName(PermissionCode),
                    ["CurrentRole"] = AppRoles.GetDisplayName(scope.CurrentRole),
                    ["RequestPath"] = context.HttpContext.Request.Path.Value
                };

                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Account/AccessDenied.cshtml",
                    StatusCode = StatusCodes.Status403Forbidden,
                    ViewData = viewData
                };
            }
        }
    }
}
