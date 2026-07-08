using System;

namespace Echo.Wire;

public record PlayerProfileResponse(ulong ContentId, string Name, uint HomeWorldId, uint CurrentWorldId, ushort LastTerritoryId, byte Level, byte JobId, string? FcTag, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt, ushort TitleId = 0, byte GrandCompany = 0, string? EquipmentJson = null, string? AvatarUrl = null, string? PortraitUrl = null, string? HomeWorldName = null);
