using TAGlobal.Api.Application.Abstractions;
using TAGlobal.Api.Contracts;
using TAGlobal.Api.Models;

namespace TAGlobal.Api.Application.UseCases;

public interface IChatUseCases
{
    Task<IReadOnlyList<object>> ListAsync(int userId, CancellationToken ct);
    Task<object> CreateAsync(CreateThreadRequest request, int userId, CancellationToken ct);
    Task<object?> SendAsync(int id, string text, int userId, CancellationToken ct);
    Task<bool> MarkReadAsync(int id, CancellationToken ct);
}

public sealed class ChatUseCases(IAdminOperationsRepository repository) : IChatUseCases
{
    public async Task<IReadOnlyList<object>> ListAsync(int userId, CancellationToken ct) =>
        (await repository.ListThreadsAsync(ct)).Select(t => ToResponse(t, userId)).ToArray();

    public async Task<object> CreateAsync(CreateThreadRequest request, int userId, CancellationToken ct)
    {
        var thread = new ChatThread { ThreadKey = Guid.NewGuid().ToString("N"), With = request.With, Role = request.Role, Subject = request.Subject, Party = request.Party };
        thread.Messages.Add(new ChatMessage { FromUserId = userId.ToString(), Text = request.Text });
        var created = await repository.CreateThreadAsync(thread, ct);
        return ToResponse(created, userId);
    }

    public async Task<object?> SendAsync(int id, string text, int userId, CancellationToken ct)
    {
        var thread = await repository.SendMessageAsync(id, userId.ToString(), text, ct);
        return thread is null ? null : ToResponse(thread, userId);
    }

    public Task<bool> MarkReadAsync(int id, CancellationToken ct) => repository.MarkThreadReadAsync(id, ct);

    private static object ToResponse(ChatThread thread, int userId) => new
    {
        id = thread.Id, with = thread.With, role = thread.Role, subject = thread.Subject, party = thread.Party, unread = thread.Unread,
        last = thread.UpdatedAtUtc.ToLocalTime().ToString("HH:mm"), online = false,
        messages = thread.Messages.OrderBy(m => m.SentAtUtc).Select(m => new { from = m.FromUserId == userId.ToString() ? "me" : "them", text = m.Text, time = m.SentAtUtc.ToLocalTime().ToString("HH:mm") })
    };
}
