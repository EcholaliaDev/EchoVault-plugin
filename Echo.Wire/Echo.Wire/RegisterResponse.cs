namespace Echo.Wire;

public record RegisterResponse(string UploaderId, string ApiKey, string HmacSecret);
