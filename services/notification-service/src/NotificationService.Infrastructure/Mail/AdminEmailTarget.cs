using NotificationService.Application.Abstractions;

namespace NotificationService.Infrastructure.Mail;

public sealed class AdminEmailTarget(string adminAddress) : IAuditEmailTarget
{
    public string AdminAddress { get; } = adminAddress;
}
