using TalentPilot.Common.Results;

namespace TalentPilot.Application.Feedback;

public interface IPublicFeedbackService
{
    Task<Result<SubmitPublicFeedbackResponse>> SubmitAsync(
        SubmitPublicFeedbackInput input,
        CancellationToken cancellationToken);
}

public interface IPublicFeedbackTenantResolver
{
    Task<PublicFeedbackTenant?> ResolveAsync(
        PublicFeedbackTenantQuery query,
        CancellationToken cancellationToken);
}
