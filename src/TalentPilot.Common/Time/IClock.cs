namespace TalentPilot.Common.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
