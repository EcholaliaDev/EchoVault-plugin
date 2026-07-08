using System;

namespace Echo.Wire;

public record SessionResponse(string Token, DateTimeOffset ExpiresAt, string Tier = "unverified");
