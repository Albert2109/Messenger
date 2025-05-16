
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

public abstract class BaseHub : Hub
{
  
    private static readonly ConcurrentDictionary<int, ConcurrentBag<string>> _conn = new();

    protected readonly ILogger<BaseHub> _log;
    protected BaseHub(ILogger<BaseHub> log) => _log = log;

    
    public override async Task OnConnectedAsync()
    {
        if (TryGetUserId(out var uid))
        {
            AddConn(uid, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, uid.ToString());

            
            foreach (var g in await ResolveExtraGroups(uid))
                await Groups.AddToGroupAsync(Context.ConnectionId, g);

            await Clients.All.SendAsync("UserOnline", uid);
            _log.LogInformation("User {uid} connected ({cid})", uid, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }

  
    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        if (TryGetUserId(out var uid))
        {
            RemoveConn(uid, Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, uid.ToString());

            if (!HasActive(uid))
                await Clients.All.SendAsync("UserOffline", uid);

            _log.LogInformation("User {uid} disconnected ({cid})", uid, Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(ex);
    }

    
    protected virtual Task<IEnumerable<string>> ResolveExtraGroups(int userId) =>
        Task.FromResult(Enumerable.Empty<string>());

   
    private bool TryGetUserId(out int uid)
        => int.TryParse(Context.GetHttpContext()?.Request.Query["userId"], out uid);

    private static void AddConn(int uid, string cid) =>
        _conn.AddOrUpdate(uid,
            _ => new ConcurrentBag<string> { cid },
            (_, bag) => { bag.Add(cid); return bag; });

    private static void RemoveConn(int uid, string cid)
    {
        if (!_conn.TryGetValue(uid, out var bag)) return;
        var rest = new ConcurrentBag<string>(bag.Where(x => x != cid));
        if (rest.Any()) _conn[uid] = rest;
        else _conn.TryRemove(uid, out _);
    }

    private static bool HasActive(int uid) =>
        _conn.TryGetValue(uid, out var bag) && bag.Any();
}
