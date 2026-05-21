# Authentication

- MVP login is card-based from the Angular page, but still uses `POST /api/auth/login`.
- `AuthService` resolves the same profile path used by production auth:
  - user identity from `AppUsers`
  - tenant from `Tenants`
  - assigned roles from `UserRoles` + `Roles`
  - effective permissions from `RolePermissions` and `TenantAccessPolicies.PermissionResolutionMode`
  - workflow routing groups from `GroupMembers` + `Groups`
- `CurrentUserContext.roles[].code` is the stable value for authorization checks.
- `CurrentUserContext.roles[].displayName` and `roleDisplayName` are UI labels only.
- `roleDisplayName` is derived from the assigned role with the lowest priority number.
- `CurrentUserContext.permissions` is the source of truth for frontend route/action checks.
- Permission ids are seeded in `scripts/seed/001_seed_initial_data.sql`, represented in `TalentPilot.Domain.Access.AccessConstants`, and mirrored by Angular `src/app/core/permissions.ts`.
- `RouteAccessMapper` returns coarse allowed route prefixes for navigation; detailed page/action checks still use permission ids.
- Groups route work only; groups do not grant permissions.
- Access tokens include role code claims and permission claims.
- Refresh tokens are stored hashed in `RefreshTokens` and revoked on logout/refresh rotation.
- Demo card login accepts a null password only when `Auth:AllowDemoCardLogin=true`.
- Production credentials can use the same endpoint after password hashing or external identity is enabled.
