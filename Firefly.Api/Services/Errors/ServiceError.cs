namespace Firefly.Api.Services.Errors;

public sealed record ServiceError(string Code, string Description, IReadOnlyList<string>? Errors = null)
{
    public static readonly ServiceError None = new(string.Empty, string.Empty);

    public IReadOnlyList<string> Errors { get; init; } = Errors ?? Array.Empty<string>();
}