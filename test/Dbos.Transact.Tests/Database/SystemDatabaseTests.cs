using System.Data;
using System.Data.Common;
using Dbos.Transact.Database;
using Dbos.Transact.Database.Daos;
using Dbos.Transact.Workflow;
using Dbos.Transact.Workflow.Internal;

namespace Dbos.Transact.Tests.Database;

public sealed class SystemDatabaseTests
{
    /// <summary>
    /// Minimal concrete SystemDatabase that delegates all DAO methods to always-throw stubs,
    /// but whose WorkflowDao can be overridden via constructor.
    /// </summary>
    private sealed class TestSystemDatabase(WorkflowDao workflowDao) : SystemDatabase
    {
        protected override WorkflowDao WorkflowDao { get; } = workflowDao;
        protected override StepsDao StepsDao { get; } = new NoOpStepsDao();
        protected override QueuesDao QueuesDao { get; } = new NoOpQueuesDao();
        protected override NotificationsDao NotificationsDao { get; } = new NoOpNotificationsDao();
        protected override SchedulesDao SchedulesDao { get; } = new NoOpSchedulesDao();
        protected override StreamsDao StreamsDao { get; } = new NoOpStreamsDao();

        protected override Task<DbConnection> OpenConnectionAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public override Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetWorkflowStatusAsync_DelegatesToWorkflowDao()
    {
        var expected = new WorkflowStatus(
            WorkflowId: "wf-1", Status: WorkflowState.Success, WorkflowName: "MyWorkflow",
            ClassName: null, InstanceName: null, AuthenticatedUser: null, AssumedRole: null,
            AuthenticatedRoles: null, Input: null, Output: null, Error: null, ExecutorId: null,
            CreatedAt: null, UpdatedAt: null, AppVersion: null, AppId: null,
            RecoveryAttempts: null, QueueName: null, Timeout: null, Deadline: null,
            StartedAt: null, DeduplicationId: null, Priority: null, QueuePartitionKey: null,
            ForkedFrom: null, ParentWorkflowId: null, WasForkedFrom: null,
            DelayUntil: null, Serialization: null);
        var dao = new FixedWorkflowDao(expected);
        var db = new TestSystemDatabase(dao);

        var result = await db.GetWorkflowStatusAsync("wf-1");

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task DbRetry_RetriesOnTransientFailure()
    {
        var dao = new FailThenSucceedWorkflowDao(failCount: 2);
        var db = new RetryableTestSystemDatabase(dao);

        // Should succeed despite 2 transient failures
        var result = await db.GetWorkflowStatusAsync("wf-1");
        Assert.NotNull(result);
        Assert.Equal(3, dao.CallCount);
    }

    // ── DAO stubs ─────────────────────────────────────────────────────────────

    private sealed class FixedWorkflowDao(WorkflowStatus? status) : WorkflowDao
    {
        public override Task<WorkflowStatus?> GetWorkflowStatusAsync(string workflowId, CancellationToken ct = default) =>
            Task.FromResult(status);

        public override Task<WorkflowInitResult> InitWorkflowStatusAsync(WorkflowStatusInternal i, int m, bool r, bool d, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task RecordWorkflowOutputAsync(string w, string? o, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task RecordWorkflowErrorAsync(string w, string? e, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<string?> GetWorkflowSerializationAsync(string w, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<string?> GetWorkflowInputsAsync(string w, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<WorkflowStatus>> ListWorkflowsAsync(ListWorkflowsInput i, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<WorkflowAggregateRow>> GetWorkflowAggregatesAsync(GetWorkflowAggregatesInput i, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<WorkflowStatus>> GetPendingWorkflowsAsync(IReadOnlyList<string> e, string? v, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task RecordChildWorkflowAsync(IDbConnection c, string w, int f, string ch, string? s, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<string?> CheckChildWorkflowAsync(string w, int f, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task CancelWorkflowsAsync(IReadOnlyList<string> w, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task ResumeWorkflowsAsync(IReadOnlyList<string> w, string? q, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task DeleteWorkflowsAsync(IReadOnlyList<string> w, bool d, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<string> ForkWorkflowAsync(string o, int s, ForkOptions f, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task TransitionDelayedWorkflowsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FailThenSucceedWorkflowDao(int failCount) : WorkflowDao
    {
        public int CallCount { get; private set; }

        public override Task<WorkflowStatus?> GetWorkflowStatusAsync(string workflowId, CancellationToken ct = default)
        {
            CallCount++;
            if (CallCount <= failCount)
                throw new TransientDbException("transient");
            var status = new WorkflowStatus(
                WorkflowId: workflowId, Status: WorkflowState.Success, WorkflowName: "Wf",
                ClassName: null, InstanceName: null, AuthenticatedUser: null, AssumedRole: null,
                AuthenticatedRoles: null, Input: null, Output: null, Error: null, ExecutorId: null,
                CreatedAt: null, UpdatedAt: null, AppVersion: null, AppId: null,
                RecoveryAttempts: null, QueueName: null, Timeout: null, Deadline: null,
                StartedAt: null, DeduplicationId: null, Priority: null, QueuePartitionKey: null,
                ForkedFrom: null, ParentWorkflowId: null, WasForkedFrom: null,
                DelayUntil: null, Serialization: null);
            return Task.FromResult<WorkflowStatus?>(status);
        }

        public override Task<WorkflowInitResult> InitWorkflowStatusAsync(WorkflowStatusInternal i, int m, bool r, bool d, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task RecordWorkflowOutputAsync(string w, string? o, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task RecordWorkflowErrorAsync(string w, string? e, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<string?> GetWorkflowSerializationAsync(string w, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<string?> GetWorkflowInputsAsync(string w, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<WorkflowStatus>> ListWorkflowsAsync(ListWorkflowsInput i, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<WorkflowAggregateRow>> GetWorkflowAggregatesAsync(GetWorkflowAggregatesInput i, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<WorkflowStatus>> GetPendingWorkflowsAsync(IReadOnlyList<string> e, string? v, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task RecordChildWorkflowAsync(IDbConnection c, string w, int f, string ch, string? s, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<string?> CheckChildWorkflowAsync(string w, int f, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task CancelWorkflowsAsync(IReadOnlyList<string> w, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task ResumeWorkflowsAsync(IReadOnlyList<string> w, string? q, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task DeleteWorkflowsAsync(IReadOnlyList<string> w, bool d, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<string> ForkWorkflowAsync(string o, int s, ForkOptions f, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task TransitionDelayedWorkflowsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class TransientDbException(string message) : Exception(message);

    private sealed class RetryableTestSystemDatabase(WorkflowDao dao) : SystemDatabase
    {
        protected override WorkflowDao WorkflowDao { get; } = dao;
        protected override StepsDao StepsDao { get; } = new NoOpStepsDao();
        protected override QueuesDao QueuesDao { get; } = new NoOpQueuesDao();
        protected override NotificationsDao NotificationsDao { get; } = new NoOpNotificationsDao();
        protected override SchedulesDao SchedulesDao { get; } = new NoOpSchedulesDao();
        protected override StreamsDao StreamsDao { get; } = new NoOpStreamsDao();

        protected override Task<DbConnection> OpenConnectionAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public override Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

        protected override bool IsRetryable(Exception exception) => exception is TransientDbException;
    }

    // ── No-op DAO stubs for unused slots ─────────────────────────────────────

    private sealed class NoOpStepsDao : StepsDao
    {
        public override Task<StepResult> CheckStepExecutionTxnAsync(IDbConnection c, string w, int f, string n, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task RecordStepResultTxnAsync(IDbConnection c, StepResult r, long s, long e, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<StepInfo>> ListWorkflowStepsAsync(string w, bool l, int? lim, int? o, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task SleepAsync(string w, int f, TimeSpan d, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class NoOpQueuesDao : QueuesDao
    {
        public override Task<bool> ClearQueueAssignmentAsync(string w, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<string>> GetQueuePartitionsAsync(string q, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<string>> GetAndStartQueuedWorkflowsAsync(Queue q, string e, string? v, string? p, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class NoOpNotificationsDao : NotificationsDao
    {
        public override Task SendAsync(string w, int s, string d, object? m, string? t, string? id, string? ser, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task SendDirectAsync(string d, object? m, string? t, string? id, string? ser, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<object?> RecvAsync(string w, int s, int ts, string? t, TimeSpan? to, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task SetEventAsync(string w, int f, string k, object? m, bool a, string? ser, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<object?> GetEventAsync(string t, string k, TimeSpan? to, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<NotificationInfo?> GetNotificationInfoAsync(string w, string k, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class NoOpSchedulesDao : SchedulesDao
    {
        public override Task CreateScheduleAsync(WorkflowSchedule s, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyList<WorkflowSchedule>> ListSchedulesAsync(IReadOnlyList<ScheduleStatus>? st, IReadOnlyList<string>? wn, IReadOnlyList<string>? sp, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<WorkflowSchedule?> GetScheduleAsync(string n, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task PauseScheduleAsync(string n, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task ResumeScheduleAsync(string n, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task UpdateScheduleLastFiredAtAsync(string n, DateTimeOffset l, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task DeleteScheduleAsync(string n, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task ApplySchedulesAsync(IReadOnlyList<WorkflowSchedule> s, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class NoOpStreamsDao : StreamsDao
    {
        public override Task WriteStreamFromStepAsync(string w, int f, string k, object? v, string? s, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task WriteStreamFromWorkflowAsync(string w, int f, string k, object? v, string? s, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task CloseStreamAsync(string w, int f, string k, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<object?> ReadStreamAsync(string w, string k, int o, CancellationToken ct = default) => throw new NotImplementedException();
        public override Task<IReadOnlyDictionary<string, IReadOnlyList<object?>>> GetAllStreamEntriesAsync(string w, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
