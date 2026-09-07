using EventService.Api.Auth;
using EventService.Application.Abstractions;
using EventService.Application.Commands;
using EventService.Application.Dtos;
using EventService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EventService.Api.Controllers;

[ApiController]
[Route("api/v1/events")]
[Authorize]
[EnableRateLimiting("user-or-ip")]
public sealed class EventsController(ISender mediator, CurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = ApiPolicies.AdministratorOrOrganizer)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var ownerId = currentUser.SubjectId;
        if (ownerId is null)
        {
            return Unauthorized();
        }

        var zones = request.Zones
            .Select(z => new CreateZoneCommand(z.Name, z.Price, z.Capacity))
            .ToList();

        var result = await mediator.Send(new CreateEventCommand(
            request.Name, request.Date, request.Venue, request.Status, ownerId.Value, zones), ct);

        var created = await mediator.Send(new GetEventQuery(result.EventId, EventScope.Admin, ownerId), ct);

        return CreatedAtAction(nameof(Get), new { id = result.EventId }, created?.Event);
    }

    [HttpGet]
    [ProducesResponseType(typeof(EventListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? after,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var scope = ResolveScope(currentUser.DominantRole);
        var result = await mediator.Send(new ListEventsQuery(
            scope,
            currentUser.SubjectId,
            status,
            after,
            Math.Clamp(limit, 1, 100)), ct);

        Response.Headers["X-Cache"] = result.ServedFromCache ? "HIT" : "MISS";
        return Ok(result.Response);
    }

    [HttpGet("{id:guid}", Name = "GetEvent")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetEventQuery(id, ResolveScope(currentUser.DominantRole), currentUser.SubjectId), ct);
        return result is null ? NotFound() : Ok(result.Event);
    }

    private static EventScope ResolveScope(string role) => role switch
    {
        "admin" => EventScope.Admin,
        "client" => EventScope.Client,
        _ => EventScope.Organizer,
    };
}
