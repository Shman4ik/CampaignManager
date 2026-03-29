namespace CampaignManager.Web.Utilities;

public sealed class Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess => Error is null;

    private Result(T value) => Value = value;
    private Result(string error) => Error = error;

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error) => new(error);
}

public sealed class Result
{
    public string? Error { get; }
    public bool IsSuccess => Error is null;

    private Result() { }
    private Result(string error) => Error = error;

    public static Result Ok() => new();
    public static Result Fail(string error) => new(error);
}
