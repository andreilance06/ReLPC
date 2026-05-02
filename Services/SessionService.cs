using ReLPC.Models;

namespace ReLPC.Services;

public interface ISessionService
{
    UserProfile? CurrentUser { get; set; }
}

public class SessionService : ISessionService
{
    public UserProfile? CurrentUser { get; set; }
}