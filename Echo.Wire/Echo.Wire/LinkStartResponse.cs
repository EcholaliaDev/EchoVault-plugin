using System;

namespace Echo.Wire;

public record LinkStartResponse(string Code, DateTimeOffset ExpiresAt);
