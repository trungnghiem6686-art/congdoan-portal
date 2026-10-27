namespace CongDoan.Application.Shared;

/// <summary>Single source of "now". Code never calls DateTime.UtcNow directly.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
