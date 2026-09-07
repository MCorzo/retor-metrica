namespace NotificationService.Application.Abstractions;

/// <summary>Target recipient for notification emails (MVP: single Admin address).</summary>
public interface IAuditEmailTarget
{
    string AdminAddress { get; }
}
