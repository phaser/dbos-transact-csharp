namespace Dbos.Transact.Json;

/// <summary>
/// Exception raised when a workflow or step fails with a structured error payload.
/// Carries an <see cref="ErrorName"/> (the logical error type), an optional numeric
/// <see cref="Code"/>, and arbitrary <see cref="Data"/>. Port of Java's
/// <c>PortableWorkflowException</c>.
/// </summary>
public sealed class PortableWorkflowException : Exception
{
    public string ErrorName { get; }
    public object? Code { get; }
    /// <summary>Arbitrary structured payload. Named <c>ErrorPayload</c> to avoid collision with <see cref="Exception.Data"/>.</summary>
    public object? ErrorPayload { get; }

    public PortableWorkflowException(string errorName, string? message, object? code = null, object? errorPayload = null)
        : base(message)
    {
        ErrorName = errorName;
        Code = code;
        ErrorPayload = errorPayload;
    }

    public static PortableWorkflowException FromErrorData(JsonWorkflowErrorData errorData) =>
        new(errorData.Name, errorData.Message, errorData.Code, errorData.Data);

    public JsonWorkflowErrorData ToErrorData() =>
        new(ErrorName, Message, Code, ErrorPayload);
}
