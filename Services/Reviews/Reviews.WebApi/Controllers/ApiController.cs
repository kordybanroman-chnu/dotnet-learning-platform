using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Reviews.WebApi.Controllers;

[ApiController]
public abstract class ApiController(ISender mediator) : ControllerBase
{
    protected ISender Mediator { get; } = mediator;
}
