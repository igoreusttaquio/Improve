using System.Text;
using Improve.Core.Providers;
using Improve.Core.Tests.Fakes;

namespace Improve.Core.Tests.Providers;

public class SseReaderTests
{
    private static async Task<List<SseEvent>> ReadAll(string body, int chunkSize)
    {
        var events = new List<SseEvent>();
        await foreach (var e in SseReader.ReadAsync(new ChunkedStream(Encoding.UTF8.GetBytes(body), chunkSize), TestContext.Current.CancellationToken))
            events.Add(e);
        return events;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4096)]
    public async Task Reassembles_events_split_across_chunks(int chunkSize)
    {
        const string body = "event: a\ndata: {\"x\":\"olá ação\"}\n\nevent: b\ndata: 2\n\n";

        var events = await ReadAll(body, chunkSize);

        Assert.Equal([new SseEvent("a", "{\"x\":\"olá ação\"}"), new SseEvent("b", "2")], events);
    }

    [Fact]
    public async Task Handles_crlf_comments_and_multiline_data()
    {
        const string body = ": ping\r\ndata: linha 1\r\ndata: linha 2\r\n\r\n";

        var events = await ReadAll(body, 2);

        Assert.Equal([new SseEvent(null, "linha 1\nlinha 2")], events);
    }

    [Fact]
    public async Task Dispatches_last_event_without_trailing_blank_line()
    {
        var events = await ReadAll("data: [DONE]", 5);

        Assert.Equal([new SseEvent(null, "[DONE]")], events);
    }

    [Fact]
    public async Task Ignores_blank_lines_without_data()
    {
        var events = await ReadAll("\n\nevent: x\n\ndata: 1\n\n", 3);

        Assert.Equal([new SseEvent(null, "1")], events);
    }
}
