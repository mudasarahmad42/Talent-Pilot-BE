using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using TalentPilot.Application.Admin.TenantProfiles;
using TalentPilot.Domain.Access;

namespace TalentPilot.Api.Security;

public sealed class AdminCenterReadOnlyFilter : IAsyncActionFilter
{
    private readonly IAdminCenterAccessPolicyReader _policyReader;
    private readonly ILogger<AdminCenterReadOnlyFilter> _logger;

    public AdminCenterReadOnlyFilter(
        IAdminCenterAccessPolicyReader policyReader,
        ILogger<AdminCenterReadOnlyFilter> logger)
    {
        _policyReader = policyReader;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method) ||
            context.HttpContext.User.IsInRole(AccessConstants.SystemAdminRoleCode))
        {
            await next();
            return;
        }

        var tenantIdValue = context.HttpContext.User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdValue, out var tenantId) || tenantId == Guid.Empty)
        {
            context.Result = new ObjectResult(new
            {
                error = "admin_center.tenant_context_required",
                message = "Tenant context is required for Admin Center changes."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        string accessMode;
        try
        {
            accessMode = await _policyReader.GetAdminCenterAccessModeAsync(
                tenantId,
                context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin Center access mode could not be evaluated for tenant {TenantId}.", tenantId);
            await next();
            return;
        }

        if (!string.Equals(accessMode, AdminCenterAccessModes.ReadOnly, StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new
        {
            error = "admin_center.read_only",
            message = "A system administrator has made Admin Center view-only for this tenant. Contact your system administrator for assistance."
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
