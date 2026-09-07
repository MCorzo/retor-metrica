namespace NotificationService.Application.Abstractions;

public interface IAuditEmailTarget
{
    string AdminAddress { get; }
}
