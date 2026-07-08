using System;

namespace Echo.Core;

public record StoredCredentials(string UploaderId, string ApiKey, string HmacSecretBase64, string? SessionToken, DateTimeOffset? SessionExpiresAt, string? Tier = null);
