using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Echo.Wire;

namespace Echo.Core;

public sealed class EchoApiClient(HttpClient http, KeyStore keys)
{
	private static readonly TimeSpan SessionRefreshMargin = TimeSpan.FromHours(1.0);

	public string? CurrentTier => keys.Load()?.Tier;

	public async Task<bool> EnsureRegisteredAsync(string pluginVersion, CancellationToken ct)
	{
		if ((object)keys.Load() != null)
		{
			return true;
		}
		using HttpResponseMessage response = await http.PostAsync("/v1/auth/register", JsonBody(new RegisterRequest(2, pluginVersion)), ct);
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}
		RegisterResponse creds = await ReadAsync<RegisterResponse>(response, ct);
		if ((object)creds == null)
		{
			return false;
		}
		keys.Save(new StoredCredentials(creds.UploaderId, creds.ApiKey, creds.HmacSecret, null, null, "unverified"));
		return true;
	}

	public async Task<bool> EnsureSessionAsync(CancellationToken ct)
	{
		StoredCredentials creds = keys.Load();
		if ((object)creds == null)
		{
			return false;
		}
		if (creds.SessionToken != null)
		{
			DateTimeOffset? sessionExpiresAt = creds.SessionExpiresAt;
			if (sessionExpiresAt.HasValue)
			{
				DateTimeOffset expiry = sessionExpiresAt.GetValueOrDefault();
				if (expiry - DateTimeOffset.UtcNow > SessionRefreshMargin && creds.Tier != null)
				{
					return true;
				}
			}
		}
		using HttpResponseMessage response = await SendSignedAsync("/v1/auth/session", new SessionRequest(2, creds.UploaderId, creds.ApiKey), creds, null, ct);
		if (!response.IsSuccessStatusCode)
		{
			return false;
		}
		SessionResponse session = await ReadAsync<SessionResponse>(response, ct);
		if ((object)session == null)
		{
			return false;
		}
		keys.Save(creds with
		{
			SessionToken = session.Token,
			SessionExpiresAt = session.ExpiresAt,
			Tier = session.Tier
		});
		return true;
	}

	public async Task<EchoApiResult> IngestAsync(IngestBatch batch, CancellationToken ct)
	{
		StoredCredentials creds = keys.Load();
		if (creds?.SessionToken == null)
		{
			return new EchoApiResult(Success: false, 0);
		}
		int firstStatus;
		using (HttpResponseMessage first = await SendSignedAsync("/v1/ingest", batch, creds, creds.SessionToken, ct))
		{
			if (first.StatusCode != HttpStatusCode.Unauthorized)
			{
				return new EchoApiResult(first.IsSuccessStatusCode, (int)first.StatusCode);
			}
			firstStatus = (int)first.StatusCode;
		}
		keys.Save(creds with
		{
			SessionToken = null,
			SessionExpiresAt = null
		});
		if (!(await EnsureSessionAsync(ct)))
		{
			return new EchoApiResult(Success: false, firstStatus);
		}
		creds = keys.Load();
		using HttpResponseMessage second = await SendSignedAsync("/v1/ingest", batch, creds, creds.SessionToken, ct);
		return new EchoApiResult(second.IsSuccessStatusCode, (int)second.StatusCode);
	}

	public async Task<VerifyStartResponse?> VerifyStartAsync(VerifyStartRequest req, CancellationToken ct)
	{
		StoredCredentials creds = keys.Load();
		if ((object)creds == null)
		{
			return null;
		}
		using HttpResponseMessage response = await SendSignedAsync("/v1/auth/verify/start", req, creds, null, ct);
		return (!response.IsSuccessStatusCode) ? null : (await ReadAsync<VerifyStartResponse>(response, ct));
	}

	public async Task<VerifyCompleteResponse?> VerifyCompleteAsync(CancellationToken ct)
	{
		StoredCredentials creds = keys.Load();
		if ((object)creds == null)
		{
			return null;
		}
		using HttpResponseMessage response = await SendSignedAsync("/v1/auth/verify/complete", new VerifyCompleteRequest(2), creds, null, ct);
		return (!response.IsSuccessStatusCode) ? null : (await ReadAsync<VerifyCompleteResponse>(response, ct));
	}

	public async Task<LinkStartResult> LinkStartAsync(LinkStartRequest req, CancellationToken ct)
	{
		StoredCredentials creds = keys.Load();
		if ((object)creds == null)
		{
			return new LinkStartResult(null, new LinkStartError("not_registered"));
		}
		using HttpResponseMessage response = await SendSignedAsync("/v1/claims/link/start", req, creds, null, ct);
		if (response.IsSuccessStatusCode)
		{
			LinkStartResponse ok = await ReadAsync<LinkStartResponse>(response, ct);
			return ((object)ok == null) ? new LinkStartResult(null, new LinkStartError("unreachable")) : new LinkStartResult(ok, null);
		}
		LinkStartError error = await ReadAsync<LinkStartError>(response, ct);
		LinkStartError error3;
		if ((object)error != null)
		{
			string error2 = error.Error;
			if (error2 != null && error2.Length > 0)
			{
				error3 = error;
				goto IL_0275;
			}
		}
		error3 = new LinkStartError("unreachable");
		goto IL_0275;
		IL_0275:
		return new LinkStartResult(null, error3);
	}

	public async Task<ConfigResponse?> GetConfigAsync(CancellationToken ct)
	{
		using HttpResponseMessage response = await http.GetAsync("/v1/config", ct);
		return (!response.IsSuccessStatusCode) ? null : (await ReadAsync<ConfigResponse>(response, ct));
	}

	private async Task<HttpResponseMessage> SendSignedAsync(string path, object body, StoredCredentials creds, string? session, CancellationToken ct)
	{
		string json = JsonSerializer.Serialize(body, body.GetType(), WireJson.Options);
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		string nonce = HmacSigner.NewNonce();
		string canonical = HmacSigner.Canonical("POST", path, HmacSigner.Sha256Hex(bytes), ts, nonce);
		byte[] secret = Convert.FromBase64String(creds.HmacSecretBase64);
		HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, path)
		{
			Content = new ByteArrayContent(bytes)
		};
		msg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
		{
			CharSet = "utf-8"
		};
		msg.Headers.Add("X-Echo-KeyId", creds.UploaderId);
		msg.Headers.Add("X-Echo-Timestamp", ts.ToString());
		msg.Headers.Add("X-Echo-Nonce", nonce);
		msg.Headers.Add("X-Echo-Signature", HmacSigner.Sign(secret, canonical));
		if (session != null)
		{
			msg.Headers.Add("X-Echo-Session", session);
		}
		return await http.SendAsync(msg, ct);
	}

	private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct) where T : class
	{
		try
		{
			return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(ct), WireJson.Options);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static StringContent JsonBody<T>(T value)
	{
		return new StringContent(JsonSerializer.Serialize(value, WireJson.Options), Encoding.UTF8, "application/json");
	}
}
