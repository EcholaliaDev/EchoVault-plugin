using System;
using Echo.Wire;

namespace Echo.Core;

public record PluginStateSnapshot(RegistrationStatus Registration, int OutboxDepth, DateTimeOffset? LastUploadAt, string? LastError, bool CaptureEnabled, bool ServerAllowsIngest, ReporterSelf? Reporter = null, bool SocialCaptureEnabled = true, bool NameCacheCaptureEnabled = true);
