using System;
using System.Collections.Generic;

namespace Echo.Wire;

public record Sighting(ulong ContentId, string Name, uint HomeWorldId, uint CurrentWorldId, ushort TerritoryId, float X, float Y, float Z, byte JobId, byte Level, string? FcTag, string? CustomizeBase64, DateTimeOffset SeenAtUtc, string Source = "sweep", ulong AccountId = 0uL, ushort TitleId = 0, byte GrandCompany = 0, List<EquipSlot>? Equipment = null, ulong MainhandModel = 0uL, ulong OffhandModel = 0uL, string? HomeWorldName = null);
