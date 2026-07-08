namespace Echo.Wire;

public record VerifyStartRequest(int ProtocolVersion, string LodestoneId, string CharacterName, string HomeWorldName, ulong ContentId);
