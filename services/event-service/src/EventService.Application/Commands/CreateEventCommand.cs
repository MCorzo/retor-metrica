using FluentValidation;
using MediatR;

namespace EventService.Application.Commands;

public sealed record CreateEventCommand(
    string Name,
    DateTimeOffset Date,
    string Venue,
    string Status,
    Guid OwnerId,
    IReadOnlyList<CreateZoneCommand> Zones) : IRequest<CreateEventCommandResult>;

public sealed record CreateZoneCommand(string Name, decimal Price, int Capacity);

public sealed record CreateEventCommandResult(Guid EventId);

public sealed class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Event name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Date)
            .NotEmpty()
            .GreaterThan(DateTimeOffset.UtcNow).WithMessage("Event date must be in the future.");

        RuleFor(x => x.Venue)
            .NotEmpty().WithMessage("Venue is required.")
            .MaximumLength(300);

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(x => EventService.Domain.Entities.Event.IsValidStatus(x))
            .WithMessage("Status must be 'draft' or 'published'.");

        RuleFor(x => x.Zones)
            .NotEmpty().WithMessage("At least one zone is required.")
            .Must(zones => zones.Count <= 100).WithMessage("Too many zones.");

        RuleForEach(x => x.Zones).ChildRules(z =>
        {
            z.RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
            z.RuleFor(c => c.Price).GreaterThan(0).WithMessage("Zone price must be positive.");
            z.RuleFor(c => c.Capacity).GreaterThan(0).WithMessage("Zone capacity must be positive.");
        });
    }
}
