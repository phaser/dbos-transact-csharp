using Dbos.Transact.Workflow;

namespace Dbos.Transact.Hosting;

/// <summary>
/// DI marker for a <see cref="Queue"/> registered via <c>AddDbosQueue</c>;
/// applied to the <see cref="Dbos"/> instance by <see cref="DbosHostedService"/>
/// before launch.
/// </summary>
public sealed class DbosQueueRegistration
{
    public Queue Queue { get; }

    public DbosQueueRegistration(Queue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        Queue = queue;
    }
}
