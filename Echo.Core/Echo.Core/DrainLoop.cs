using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Echo.Wire;

namespace Echo.Core;

public sealed class DrainLoop(Outbox outbox, EchoApiClient client, BackoffPolicy backoff, PluginState state, Func<DateTimeOffset>? clock = null)
{
	private readonly Func<DateTimeOffset> _clock = clock ?? ((Func<DateTimeOffset>)(() => DateTimeOffset.UtcNow));

	private DateTimeOffset _lastConfigFetch;

	public TimeSpan IdleDelay { get; set; } = TimeSpan.FromSeconds(10.0);

	public TimeSpan DisabledDelay { get; set; } = TimeSpan.FromSeconds(60.0);

	public TimeSpan ConfigRefreshInterval { get; set; } = TimeSpan.FromMinutes(15.0);

	public Action<ConfigResponse>? OnConfig { get; set; }

	public bool LastConfigPending { get; private set; } = true;

	public TimeSpan? ServerCadence { get; private set; }

	public async Task RunAsync(string pluginVersion, CancellationToken ct)
	{
		_ = 7;
		try
		{
			await FetchConfigAsync(pluginVersion, ct);
			_lastConfigFetch = _clock();
			while (!ct.IsCancellationRequested)
			{
				try
				{
					if (_clock() - _lastConfigFetch >= ConfigRefreshInterval)
					{
						await FetchConfigAsync(pluginVersion, ct);
						_lastConfigFetch = _clock();
					}
					if (!state.Snapshot().ServerAllowsIngest)
					{
						await Task.Delay(DisabledDelay, ct);
						continue;
					}
					if (!backoff.ShouldAttempt(_clock()))
					{
						await Task.Delay(IdleDelay, ct);
						continue;
					}
					if (!(await client.EnsureRegisteredAsync(pluginVersion, ct)))
					{
						Fail("registration failed");
						continue;
					}
					if (!(await client.EnsureSessionAsync(ct)))
					{
						state.SetRegistration(RegistrationStatus.Registered);
						Fail("session failed");
						continue;
					}
					PluginState pluginState = state;
					bool flag;
					switch (client.CurrentTier)
					{
					case "standard":
					case "trusted":
					case "verified":
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					pluginState.SetRegistration((!flag) ? RegistrationStatus.Registered : RegistrationStatus.Verified);
					IReadOnlyList<string> lines = outbox.Peek(200);
					if (lines.Count == 0)
					{
						await Task.Delay(IdleDelay, ct);
						continue;
					}
					List<Sighting> sightings = new List<Sighting>();
					foreach (string line in lines)
					{
						ct.ThrowIfCancellationRequested();
						try
						{
							Sighting s = JsonSerializer.Deserialize<Sighting>(line, WireJson.Options);
							if ((object)s != null)
							{
								sightings.Add(s);
							}
						}
						catch (JsonException)
						{
						}
					}
					if (sightings.Count == 0)
					{
						outbox.Commit(lines.Count);
						state.SetOutboxDepth(outbox.Count());
						continue;
					}
					ReporterSelf reporter = state.Snapshot().Reporter ?? new ReporterSelf(0, 0f, 0f, 0f);
					EchoApiResult result = await client.IngestAsync(new IngestBatch(2, pluginVersion, reporter, sightings), ct);
					if (result.Success)
					{
						outbox.Commit(lines.Count);
						backoff.RecordSuccess();
						state.SetLastUpload(_clock());
						state.SetError(null);
						state.SetOutboxDepth(outbox.Count());
					}
					else
					{
						Fail($"upload failed ({result.StatusCode})");
					}
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					break;
				}
				catch (Exception ex3)
				{
					Fail(ex3.Message);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private async Task FetchConfigAsync(string pluginVersion, CancellationToken ct)
	{
		try
		{
			ConfigResponse config = await client.GetConfigAsync(ct);
			if ((object)config != null)
			{
				Version mine;
				Version min;
				bool versionOk = Version.TryParse(pluginVersion, out mine) && Version.TryParse(config.MinPluginVersion, out min) && mine >= min;
				state.SetServerAllowsIngest(config.IngestEnabled && versionOk);
				ServerCadence = TimeSpan.FromSeconds((double)config.CaptureCadenceSeconds);
				OnConfig?.Invoke(config);
			}
		}
		catch (Exception) when (!ct.IsCancellationRequested)
		{
		}
		finally
		{
			LastConfigPending = false;
		}
	}

	private void Fail(string message)
	{
		backoff.RecordFailure(_clock());
		state.SetError(message);
	}
}
