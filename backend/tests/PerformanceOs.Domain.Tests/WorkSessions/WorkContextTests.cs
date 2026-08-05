using PerformanceOs.Domain.Common;
using PerformanceOs.Domain.WorkSessions;

namespace PerformanceOs.Domain.Tests.WorkSessions;

public class WorkContextTests
{
    private static WorkContext Create(
        WorkLocation location = WorkLocation.Home,
        string? locationNote = null,
        int meetingCount = 1,
        bool interruptionExpected = false)
        => WorkSessionFactory
            .Started(workContext: WorkSessionFactory.WorkContext(
                location, locationNote, meetingCount, interruptionExpected))
            .WorkContext;

    [Fact]
    public void 環境を記録できる()
    {
        var context = Create(WorkLocation.Office, meetingCount: 2, interruptionExpected: true);

        Assert.Equal(WorkLocation.Office, context.WorkLocation);
        Assert.Null(context.LocationNote);
        Assert.Equal(2, context.MeetingCount);
        Assert.True(context.InterruptionExpected);
    }

    /// <summary>WC-1</summary>
    [Fact]
    public void 会議件数が負なら例外になる()
    {
        Assert.Throws<DomainException>(() => Create(meetingCount: -1));
    }

    [Fact]
    public void 会議件数はゼロを許す()
    {
        Assert.Equal(0, Create(meetingCount: 0).MeetingCount);
    }

    /// <summary>WC-2: 場所の補足は Other のときのみ設定できる。</summary>
    [Fact]
    public void その他の場所では補足を設定できる()
    {
        var context = Create(WorkLocation.Other, locationNote: "図書館");

        Assert.Equal("図書館", context.LocationNote);
    }

    [Theory]
    [InlineData(WorkLocation.Home)]
    [InlineData(WorkLocation.Office)]
    [InlineData(WorkLocation.Cafe)]
    public void その他以外の場所で補足を設定すると例外になる(WorkLocation location)
    {
        Assert.Throws<DomainException>(() => Create(location, locationNote: "図書館"));
    }

    [Theory]
    [InlineData(WorkLocation.Home)]
    [InlineData(WorkLocation.Other)]
    public void 空白のみの補足はnullになり例外にならない(WorkLocation location)
    {
        Assert.Null(Create(location, locationNote: "   ").LocationNote);
    }

    [Fact]
    public void 補足が上限を超えると例外になる()
    {
        var note = new string('あ', WorkContext.MaxLocationNoteLength + 1);

        Assert.Throws<DomainException>(() => Create(WorkLocation.Other, locationNote: note));
    }
}
