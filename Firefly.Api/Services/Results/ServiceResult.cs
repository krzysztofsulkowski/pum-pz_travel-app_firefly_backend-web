using System.Net;
using System.Runtime.CompilerServices;
using Firefly.Api.Services.Errors;

namespace Firefly.Api.Services.Results;

public class ServiceResult
{
    private ServiceResult(bool isSuccess, ServiceError error, HttpStatusCode? statusCode = null)
    {
        if ((isSuccess && error != ServiceError.None) || (!isSuccess && error == ServiceError.None))
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public HttpStatusCode? StatusCode { get; }
    public ServiceError Error { get; }

    public static ServiceResult Success()
        => new(true, ServiceError.None);

    public static ServiceResult Failure(ServiceError error, HttpStatusCode? statusCode = null)
        => new(false, error, statusCode);

    public static ServiceResult Failure(string error, [CallerMemberName] string caller = "Generic")
        => new(false, new ServiceError(caller, error));

    public static ServiceResult NotFound(ServiceError error)
        => new(false, error, HttpStatusCode.NotFound);
}

public class ServiceResult<T>
{
    private ServiceResult(T? data, bool isSuccess, ServiceError error, HttpStatusCode? statusCode = null)
    {
        if ((isSuccess && error != ServiceError.None) || (!isSuccess && error == ServiceError.None))
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
        Data = data!;
        StatusCode = statusCode;
    }

    public T Data { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public HttpStatusCode? StatusCode { get; }
    public ServiceError Error { get; }

    public static ServiceResult<T> Success(T data)
        => new(data, true, ServiceError.None);

    public static ServiceResult<T> Failure(ServiceError error, HttpStatusCode? statusCode = null)
        => new(default, false, error, statusCode);

    public static ServiceResult<T> Failure(string error, [CallerMemberName] string caller = "Generic")
        => new(default, false, new ServiceError(caller, error));

    public static ServiceResult<T> NotFound(ServiceError error)
        => new(default, false, error, HttpStatusCode.NotFound);
}