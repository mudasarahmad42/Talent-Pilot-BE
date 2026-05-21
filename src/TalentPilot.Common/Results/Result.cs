namespace TalentPilot.Common.Results;

public class Result
{
    protected Result(bool succeeded, Error error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }

    public bool Failed => !Succeeded;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(string code, string message) => new(false, new Error(code, message));
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(true, Error.None)
    {
        _value = value;
    }

    private Result(Error error)
        : base(false, error)
    {
    }

    public T Value => Succeeded
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(string code, string message) => new(new Error(code, message));
}
