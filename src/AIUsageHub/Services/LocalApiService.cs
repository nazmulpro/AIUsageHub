using System.Net;
using System.Text;
using System.Text.Json;
using AIUsageHub.Models;

namespace AIUsageHub.Services;

public sealed class LocalApiService : IDisposable
{
    private readonly CacheService _cache;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private const string Prefix = "http://127.0.0.1:6736/";

    public LocalApiService(CacheService cache) => _cache = cache;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add(Prefix);
        _listener.Start();
        Task.Run(() => ListenAsync(_cts.Token));
        System.Diagnostics.Debug.WriteLine($"[LocalApi] Listening on {Prefix}");
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var ctx = await _listener!.GetContextAsync();
                _ = HandleRequestAsync(ctx, ct);
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalApi] Listen error: {ex.Message}");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

            if (ctx.Request.Url?.AbsolutePath == "/v1/usage")
            {
                var result = BuildUsageResponse();
                var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                var bytes = Encoding.UTF8.GetBytes(json);
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes, ct);
            }
            else
            {
                ctx.Response.StatusCode = 404;
                var msg = Encoding.UTF8.GetBytes("{\"error\":\"not found\"}");
                ctx.Response.ContentLength64 = msg.Length;
                await ctx.Response.OutputStream.WriteAsync(msg, ct);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalApi] Request error: {ex.Message}");
        }
        finally
        {
            ctx.Response.OutputStream.Close();
        }
    }

    private object BuildUsageResponse()
    {
        // Collect all cached snapshots
        var providers = new List<object>();
        foreach (var providerName in _cache.GetCachedProviderNames())
        {
            var snapshot = _cache.Get(providerName);
            if (snapshot == null) continue;
            providers.Add(new
            {
                provider = snapshot.ProviderName,
                plan = snapshot.Plan,
                error = snapshot.Error,
                refreshedAt = snapshot.RefreshedAt,
                metrics = snapshot.Lines.Select(l => new
                {
                    kind = l.Kind.ToString().ToLower(),
                    label = l.Label,
                    used = l.Kind == MetricKind.Progress ? (double?)l.Used : null,
                    limit = l.Kind == MetricKind.Progress ? (double?)l.Limit : null,
                    format = l.Kind == MetricKind.Progress ? l.Format.ToString().ToLower() : null,
                    resetsAt = l.ResetsAt,
                    values = l.MetricValues?.Select(v => new
                    {
                        value = v.Value,
                        kind = v.Kind.ToString().ToLower(),
                        unit = v.Unit
                    }),
                    text = l.TextValue,
                    badge = l.BadgeText
                })
            });
        }

        return new { providers, fetchedAt = DateTime.UtcNow };
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _listener?.Close();
    }
}