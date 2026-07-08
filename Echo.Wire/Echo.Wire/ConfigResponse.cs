namespace Echo.Wire;

public record ConfigResponse(string MinPluginVersion, int CaptureCadenceSeconds, bool IngestEnabled, int MinEmitIntervalSeconds = 10);
