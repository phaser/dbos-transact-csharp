namespace Dbos.Transact.Workflow;

/// <summary>
/// Specifies a portable, language-agnostic class name for workflow registration, overriding the
/// runtime type name. Workflows on a class decorated with this attribute are registered as
/// <c>{Value}//{MethodName}</c> instead of <c>{FullTypeName}//{MethodName}</c>.
/// Port of Java <c>@WorkflowClassName</c> annotation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class WorkflowClassNameAttribute : Attribute
{
    public WorkflowClassNameAttribute(string value) => Value = value;

    /// <summary>The portable class name to use for workflow registration.</summary>
    public string Value { get; }
}
