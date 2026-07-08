using Echo.Wire;

namespace Echo.Core;

public record LinkStartResult(LinkStartResponse? Response, LinkStartError? Error);
