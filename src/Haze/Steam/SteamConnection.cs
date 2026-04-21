using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Haze.Util;
using Microsoft.Extensions.Logging;
using SteamKit2;
using SteamKit2.Authentication;

namespace Haze.Steam;

public class SteamConnection : IAsyncDisposable
{
    public SteamClient Client { get; }
    public CallbackManager Manager { get; }
    public SteamUser User { get; }

    [MemberNotNullWhen(true, nameof(AccountName), nameof(TokenSet))]
    public bool HasAuthenticated { get; protected set; } = false;
    public string? AccountName { get; protected set; }
    public SteamTokenSet? TokenSet { get; protected set; }

    [MemberNotNullWhen(true, nameof(AccountId))]
    public bool HasLoggedOn { get; protected set; } = false;
    public bool IsLoggedOn { get; protected set; }
    public SteamID? AccountId { get; protected set; }

    private readonly CancellationTokenSource _runManagerCts = new();
    private readonly Task _runManagerTask;

    private readonly IDisposable[] _subscriptions;
    private readonly ILogger _logger;
    public bool HasConnected { get; protected set; }
    public bool IsConnected => HasConnected && !HasDisconnected;

    private readonly TaskCompletionSource _disconnectedTaskSource = new();
    public bool HasDisconnected => _disconnectedTaskSource.Task.IsCompleted;

    public SteamConnection(ILogger logger)
    {
        _logger = logger;
        Client = new SteamClient();
        Manager = new CallbackManager( Client );
        User = Client.GetHandler<SteamUser>() ?? throw new InvalidOperationException();
        _subscriptions = [
            Manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected),
            Manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff)
        ];
        _runManagerTask = Task.Run(() =>
            Manager.RunForeverAsync(_runManagerCts.Token).IgnoreCancellationBy(_runManagerCts.Token),
            _runManagerCts.Token
        );
    }

    public async Task Connect()
    {
        if (HasConnected) return;
        var connectedTaskSource = new TaskCompletionSource();
        using var _ = Manager.Subscribe<SteamClient.ConnectedCallback>(_ => connectedTaskSource.SetResult());
        _logger.LogDebug("Connecting to Steam");
        Client.Connect();
        await connectedTaskSource.Task;
        HasConnected = true;
        _logger.LogDebug("Connected to Steam");
    }

    public async Task LogOn()
    {
        if (!IsConnected) throw new InvalidOperationException();
        if (!HasAuthenticated) throw new InvalidOperationException();

        var loggedOnTaskSource = new TaskCompletionSource<SteamUser.LoggedOnCallback>();
        using var _ = Manager.Subscribe<SteamUser.LoggedOnCallback>(loggedOnTaskSource.SetResult);
        User.LogOn(new SteamUser.LogOnDetails {
            LoginID = (uint)new Random().Next(int.MinValue, int.MaxValue),
            Username = AccountName,
            AccessToken = TokenSet?.RefreshToken,
            ShouldRememberPassword = true,
        });
        await loggedOnTaskSource.Task;
        var callback = loggedOnTaskSource.Task.Result;
        if (callback.Result is not EResult.OK) {
            throw new Exception($"Steam logon failed: {callback.Result} / {callback.ExtendedResult}");
        }

        AccountId = callback.ClientSteamID;
        HasLoggedOn = true;
        IsLoggedOn = true;
    }

    public async Task QrAuth(Func<QrAuthSession, Task> notifyChallengeUrl, CancellationToken ct)
    {
        if (!IsConnected) throw new InvalidOperationException();

        var authSessionDetails = new AuthSessionDetails {
            IsPersistentSession = true,
        };
        var authSession = await Client.Authentication.BeginAuthSessionViaQRAsync(authSessionDetails);

        void NotifyChallengeUrlAndForget() {
            _ = Task.Run(() => notifyChallengeUrl(authSession), ct);
        }
        authSession.ChallengeURLChanged += NotifyChallengeUrlAndForget;
        await notifyChallengeUrl(authSession);

        var authResultTaskCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var authResultTask = authSession.PollingWaitForResultAsync(authResultTaskCts.Token);
        await Task.WhenAny(authResultTask, _disconnectedTaskSource.Task);
        if (!authResultTask.IsCompleted) {
            await authResultTaskCts.CancelAsync();
            throw new AuthenticationException("Disconnected from Steam");
        }
        var result = authResultTask.Result;
        AccountName = result.AccountName;
        TokenSet = new SteamTokenSet(result.AccessToken, result.RefreshToken);
        HasAuthenticated = true;
    }

    public async Task Disconnect()
    {
        if (!IsConnected) return;
        Client.Disconnect();
        await _disconnectedTaskSource.Task;
    }

    private async ValueTask OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        _logger.LogDebug("Disconnected from Steam");
        _disconnectedTaskSource.SetResult();
    }

    private async ValueTask OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        _logger.LogDebug($"Logged off: {callback.Result}");
        IsLoggedOn = false;
    }

    public async ValueTask DisposeAsync()
    {
        await Disconnect();
        foreach (var subscription in _subscriptions) subscription.Dispose();
        await _runManagerCts.CancelAsync();
        await _runManagerTask.IgnoreCancellationBy(_runManagerCts.Token);
    }
}
