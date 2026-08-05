using PerformanceOs.Domain.Repositories;
using PerformanceOs.Domain.Time;
using PerformanceOs.Domain.ValueObjects;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Application.Tests.Fakes;

internal sealed class InMemoryWorkSessionRepository : IWorkSessionRepository
{
    private readonly List<WorkSession> _items = [];
    private long _nextId = 1;

    public IReadOnlyList<WorkSession> Items => _items;

    /// <summary>進行中のセッションを 1 件投入する。</summary>
    public WorkSession SeedActive(long taskItemId, long workTypeId, DateTimeOffset startedAt)
    {
        var session = WorkSession.Start(
            taskItemId,
            workTypeId,
            plannedWorkId: null,
            new PreWorkStateInput(new Rating(3), new Rating(3), new Rating(3)),
            new WorkContextInput(WorkLocation.Home, null, 0, false),
            startedAt);

        AssignId(session, _nextId++);
        _items.Add(session);
        return session;
    }

    public Task<WorkSession?> GetActiveAsync(CancellationToken cancellationToken)
        => Task.FromResult(_items.FirstOrDefault(x => x.Status == SessionStatus.InProgress));

    public Task<WorkSession?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<WorkSession>> GetByDateRangeAsync(
        DateOnly fromJst, DateOnly toJst, SessionStatus? status, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkSession> result = _items
            .Where(x => x.BelongingDate >= fromJst && x.BelongingDate <= toJst)
            .Where(x => status is null || x.Status == status)
            .OrderByDescending(x => x.StartedAt)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<bool> ExistsByPlannedWorkIdAsync(long plannedWorkId, CancellationToken cancellationToken)
        => Task.FromResult(_items.Any(x => x.PlannedWorkId == plannedWorkId));

    public Task AddAsync(WorkSession workSession, CancellationToken cancellationToken)
    {
        // 実 DB の uq_work_sessions_single_active を模す。
        if (workSession.Status == SessionStatus.InProgress
            && _items.Any(x => x.Status == SessionStatus.InProgress))
        {
            throw new InvalidOperationException(
                "進行中のセッションが既に存在します（uq_work_sessions_single_active 相当）。");
        }

        AssignId(workSession, _nextId++);
        _items.Add(workSession);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(WorkSession workSession, CancellationToken cancellationToken)
        => Task.CompletedTask;

    private static void AssignId(WorkSession session, long id)
        => typeof(Domain.Common.Entity)
            .GetProperty(nameof(Domain.Common.Entity.Id))!
            .SetValue(session, id);
}
