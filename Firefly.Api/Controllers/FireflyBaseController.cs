using Firefly.Api.Services.Errors;
using Firefly.Api.Services.Results;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Firefly.Api.Controllers;

public abstract class FireflyBaseController : ControllerBase
{
    protected IActionResult HandleServiceResult<T>(ServiceResult<T> serviceResult, int? customStatusCode = null)
    {
        if (serviceResult.IsSuccess)
        {
            return customStatusCode.HasValue
                ? StatusCode(customStatusCode.Value, serviceResult.Data)
                : Ok(serviceResult.Data);
        }

        return ToProblemResult(serviceResult.Error, serviceResult.StatusCode);
    }

    protected IActionResult HandleServiceResult(ServiceResult serviceResult, int? customStatusCode = null)
    {
        if (serviceResult.IsSuccess)
            return customStatusCode.HasValue ? StatusCode(customStatusCode.Value) : Ok();

        return ToProblemResult(serviceResult.Error, serviceResult.StatusCode);
    }

    private IActionResult ToProblemResult(ServiceError error, HttpStatusCode? statusCode)
    {
        var code = (int)(statusCode ?? HttpStatusCode.BadRequest);

        if (error.Errors.Count > 0)
        {
            foreach (var err in error.Errors)
                ModelState.AddModelError("Model", err);

            return ValidationProblem(
                detail: error.Description,
                statusCode: code,
                type: $"Error {error.Code}",
                modelStateDictionary: ModelState);
        }

        return Problem(
            detail: error.Description,
            statusCode: code,
            type: $"Error {error.Code}");
    }
}