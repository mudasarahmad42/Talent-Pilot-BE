using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalentPilot.Api.Security;
using TalentPilot.Domain.Access;

namespace TalentPilot.Api.Controllers.Admin;

[Authorize(Roles = AccessConstants.AdminRoleCodes)]
[ServiceFilter(typeof(AdminCenterReadOnlyFilter))]
public abstract class AdminApiControllerBase : ApiControllerBase
{
}
