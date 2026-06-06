using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using TalentPilot.Api.Security;
using TalentPilot.Application.Admin.TenantProfiles;
using TalentPilot.Domain.Access;

namespace TalentPilot.Tests.Security;

public sealed class AdminCenterReadOnlyFilterTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task OnActionExecutionAsync_BlocksTenantAdminMutation_WhenTenantIsReadOnly()
    {
        var filter = new AdminCenterReadOnlyFilter(
            new StaticPolicyReader(AdminCenterAccessModes.ReadOnly),
            NullLogger<AdminCenterReadOnlyFilter>.Instance);
        var context = CreateContext(HttpMethods.Put, TenantId, AccessConstants.TenantAdminRoleCode);
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, Next(context, () => nextCalled = true));

        Assert.False(nextCalled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Contains("view-only", result.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AllowsTenantAdminRead_WhenTenantIsReadOnly()
    {
        var filter = new AdminCenterReadOnlyFilter(
            new StaticPolicyReader(AdminCenterAccessModes.ReadOnly),
            NullLogger<AdminCenterReadOnlyFilter>.Instance);
        var context = CreateContext(HttpMethods.Get, TenantId, AccessConstants.TenantAdminRoleCode);
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, Next(context, () => nextCalled = true));

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AllowsSystemAdminMutation_WhenTenantIsReadOnly()
    {
        var filter = new AdminCenterReadOnlyFilter(
            new StaticPolicyReader(AdminCenterAccessModes.ReadOnly),
            NullLogger<AdminCenterReadOnlyFilter>.Instance);
        var context = CreateContext(HttpMethods.Put, TenantId, AccessConstants.SystemAdminRoleCode);
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, Next(context, () => nextCalled = true));

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AllowsTenantAdminMutation_WhenTenantHasFullAccess()
    {
        var filter = new AdminCenterReadOnlyFilter(
            new StaticPolicyReader(AdminCenterAccessModes.FullAccess),
            NullLogger<AdminCenterReadOnlyFilter>.Instance);
        var context = CreateContext(HttpMethods.Put, TenantId, AccessConstants.TenantAdminRoleCode);
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, Next(context, () => nextCalled = true));

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    private static ActionExecutingContext CreateContext(string method, Guid tenantId, string roleCode)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(ClaimTypes.Role, roleCode)
            ],
            authenticationType: "test"));

        return new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            [],
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static ActionExecutionDelegate Next(ActionExecutingContext context, Action markCalled)
    {
        return () =>
        {
            markCalled();
            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        };
    }

    private sealed class StaticPolicyReader : IAdminCenterAccessPolicyReader
    {
        private readonly string _mode;

        public StaticPolicyReader(string mode)
        {
            _mode = mode;
        }

        public Task<string> GetAdminCenterAccessModeAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            Assert.Equal(TenantId, tenantId);
            return Task.FromResult(_mode);
        }
    }
}
