// Hubs/ChatHub.cs
using Microsoft.Extensions.Logging;

public class ChatHub : BaseHub
{
    public ChatHub(ILogger<ChatHub> log) : base(log) { }
    
}
