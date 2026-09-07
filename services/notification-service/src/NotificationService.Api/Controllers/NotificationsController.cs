using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NotificationService.Api.Auth;
using NotificationService.Application.Dtos;
using NotificationService.Application.Queries;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize(Policy = ApiPolicies.AdministratorOrOrganizer)]
[EnableRateLimiting("user-or-ip")]
public sealed class NotificationsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationListResponse>> List(CancellationToken ct)
    {
        var response = await mediator.Send(new ListNotificationsQuery(), ct);
        return Ok(response);
    }
}
