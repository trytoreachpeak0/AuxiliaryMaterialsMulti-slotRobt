namespace AgvDispatch.Sdk.Exceptions;

public sealed class AgvDispatchException : Exception
{
    public int? HttpStatusCode { get; }
    public string? DispatchCode { get; }
    public string? DispatchMessage { get; }
    public string? RawBody { get; }

    public AgvDispatchException(string message) : base(message) { }

    public AgvDispatchException(string message, Exception inner) : base(message, inner) { }

    public AgvDispatchException(
        string message,
        int? httpStatusCode,
        string? dispatchCode,
        string? dispatchMessage,
        string? rawBody = null)
        : base(message)
    {
        HttpStatusCode = httpStatusCode;
        DispatchCode = dispatchCode;
        DispatchMessage = dispatchMessage;
        RawBody = rawBody;
    }
}
