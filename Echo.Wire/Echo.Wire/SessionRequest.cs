namespace Echo.Wire;

public record SessionRequest(int ProtocolVersion, string UploaderId, string ApiKey);
