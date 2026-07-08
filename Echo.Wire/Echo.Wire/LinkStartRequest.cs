namespace Echo.Wire;

public record LinkStartRequest(int ProtocolVersion, ulong ContentId, string CharacterName, uint HomeWorldId);
