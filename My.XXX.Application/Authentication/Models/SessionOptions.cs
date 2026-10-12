namespace My.XXX.Application.Authentication.Models;

public sealed class SessionOptions
{
    public int RefreshExpiryInMinutes { get; set; }
}

public sealed record SessionLifetime
{
    public System.TimeSpan Duration { get; }
    public SessionLifetime(int minutes)
    {
        if (minutes <= 0) throw new System.ArgumentOutOfRangeException(nameof(minutes));
        Duration = System.TimeSpan.FromMinutes(minutes);
    }
}
