using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System;
using System.Linq;

public class ChatHub : Hub
{
    private readonly ApplicationDbContext _dbContext;
    private static ConcurrentDictionary<string, string> _connections = new();

    public ChatHub(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SendMessage(string user, string message)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            user = Context.User?.Identity?.Name ?? "Anonymous";
        }

        var time = DateTime.Now.ToString("HH:mm");

        var chatMessage = new ChatMessage
        {
            User = user,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        _dbContext.ChatMessages.Add(chatMessage);
        await _dbContext.SaveChangesAsync();

        await Clients.All.SendAsync("ReceiveMessage", user, message, time);
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User?.Identity?.Name;

        if (string.IsNullOrWhiteSpace(user))
        {
            // Do not add unknown/anonymous user to the list
            await base.OnConnectedAsync();
            return;
        }

        _connections[Context.ConnectionId] = user;

        var onlineUsers = _connections.Values
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct()
            .ToList();

        await Clients.All.SendAsync("UserConnected", user, onlineUsers);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        _connections.TryRemove(Context.ConnectionId, out string user);

        var onlineUsers = _connections.Values
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct()
            .ToList();

        await Clients.All.SendAsync("UserDisconnected", user, onlineUsers);
        await base.OnDisconnectedAsync(exception);
    }
}
