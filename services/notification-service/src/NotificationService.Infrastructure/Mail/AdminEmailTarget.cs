using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Mail;

/// <summary>Resolves the single Admin recipient from configuration (MVP scope).</summary>
public sealed class AdminEmailTarget(string adminAddress) : IAuditEmailTarget
{
    public string AdminAddress { get; } = adminAddress;
}
