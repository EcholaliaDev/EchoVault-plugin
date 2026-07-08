using System;
using System.Collections.Generic;
using System.Text.Json;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using Echo.Core;
using Echo.Wire;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Echo;

internal sealed class SocialCollector(IFramework framework, IPartyList partyList, IObjectTable objectTable, IDataManager dataManager, CaptureEngine socialEngine, Outbox outbox, PluginState state, IPluginLog log) : IDisposable
{
	private static readonly TimeSpan PartyInterval = TimeSpan.FromSeconds(15L);

	private static readonly TimeSpan RosterInterval = TimeSpan.FromSeconds(60L);

	private DateTime _lastParty = DateTime.MinValue;

	private DateTime _lastRoster = DateTime.MinValue;

	public void Enable()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		framework.Update += new OnUpdateDelegate(OnUpdate);
	}

	public void Dispose()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		framework.Update -= new OnUpdateDelegate(OnUpdate);
	}

	private string? WorldName(uint worldId)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		if (worldId != 0)
		{
			World? rowOrDefault = dataManager.GetExcelSheet<World>((ClientLanguage?)null, (string)null).GetRowOrDefault(worldId);
			if (!rowOrDefault.HasValue)
			{
				return null;
			}
			World valueOrDefault = rowOrDefault.GetValueOrDefault();
			ReadOnlySeString name = ((World)(ref valueOrDefault)).Name;
			return ((ReadOnlySeString)(ref name)).ExtractText();
		}
		return null;
	}

	private unsafe void OnUpdate(IFramework _)
	{
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			PluginStateSnapshot snap = state.Snapshot();
			if (!snap.CaptureEnabled || !snap.SocialCaptureEnabled || !snap.ServerAllowsIngest)
			{
				return;
			}
			IPlayerCharacter local = objectTable.LocalPlayer;
			if (local == null)
			{
				return;
			}
			ulong localContentId = ReadLocalContentId(local);
			DateTime now = DateTime.UtcNow;
			List<CapturedPlayer> captured = new List<CapturedPlayer>();
			if (now - _lastParty >= PartyInterval)
			{
				_lastParty = now;
				InfoProxyCrossRealm* crossRealm = InfoProxyCrossRealm.Instance();
				if (crossRealm != null && ((InfoProxyCrossRealm)crossRealm).IsCrossRealm && ((InfoProxyCrossRealm)crossRealm).GroupCount > 0)
				{
					Span<CrossRealmGroup> groups = ((InfoProxyCrossRealm)crossRealm).CrossRealmGroups;
					Span<CrossRealmGroup> span = groups[..Math.Min(((InfoProxyCrossRealm)crossRealm).GroupCount, groups.Length)];
					for (int i = 0; i < span.Length; i++)
					{
						ref CrossRealmGroup reference = ref span[i];
						CrossRealmGroup val = reference;
						Span<CrossRealmMember> members = ((CrossRealmGroup)(ref val)).GroupMembers;
						Span<CrossRealmMember> span2 = members[..Math.Min(reference.GroupMemberCount, members.Length)];
						for (int j = 0; j < span2.Length; j++)
						{
							ref CrossRealmMember m = ref span2[j];
							ulong contentId = m.ContentId;
							CrossRealmMember val2 = m;
							CapturedPlayer mapped = CrossRealmCapture.TryMap(contentId, ((CrossRealmMember)(ref val2)).NameString, m.HomeWorld, m.CurrentWorld, m.ClassJobId, m.Level, (m.HomeWorld > 0) ? WorldName((uint)m.HomeWorld) : null);
							if ((object)mapped != null)
							{
								captured.Add(mapped);
							}
						}
					}
				}
				else
				{
					foreach (IPartyMember pm in (IEnumerable<IPartyMember>)partyList)
					{
						ulong contentId2 = pm.ContentId;
						uint worldId = pm.World.RowId;
						if (contentId2 != 0L && worldId != 0)
						{
							captured.Add(new CapturedPlayer(contentId2, pm.Name.TextValue, worldId, worldId, 0, 0f, 0f, 0f, (byte)pm.ClassJob.RowId, pm.Level, null, null, "social", 0uL, 0, 0, null, 0uL, 0uL, WorldName(worldId)));
						}
					}
				}
			}
			if (now - _lastRoster >= RosterInterval)
			{
				_lastRoster = now;
				CollectProxy((InfoProxyId)15, captured);
				CollectProxy((InfoProxyId)6, captured);
				CollectProxy((InfoProxyId)4, captured);
				CollectProxy((InfoProxyId)31, captured);
			}
			if (captured.Count == 0)
			{
				return;
			}
			foreach (Sighting s in socialEngine.Process(captured, localContentId, DateTimeOffset.UtcNow))
			{
				outbox.Append(JsonSerializer.Serialize(s, WireJson.Options));
			}
		}
		catch (Exception ex)
		{
			log.Verbose(ex, "Echo social collection failed", Array.Empty<object>());
		}
	}

	private unsafe void CollectProxy(InfoProxyId id, List<CapturedPlayer> captured)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		InfoModule* module = InfoModule.Instance();
		if (module == null)
		{
			return;
		}
		InfoProxyCommonList* proxy = (InfoProxyCommonList*)((InfoModule)module).GetInfoProxyById(id);
		if (proxy == null)
		{
			return;
		}
		ReadOnlySpan<CharacterData> charDataSpan = ((InfoProxyCommonList)proxy).CharDataSpan;
		for (int i = 0; i < charDataSpan.Length; i++)
		{
			ref readonly CharacterData data = ref charDataSpan[i];
			ulong contentId = data.ContentId;
			uint worldId = data.HomeWorld;
			if (contentId != 0L && worldId != 0)
			{
				CharacterData val = data;
				captured.Add(new CapturedPlayer(contentId, ((CharacterData)(ref val)).NameString, worldId, worldId, 0, 0f, 0f, 0f, data.Job, 0, null, null, "social", data.AccountId, 0, 0, null, 0uL, 0uL, WorldName(worldId)));
			}
		}
	}

	private static ulong ReadLocalContentId(IPlayerCharacter local)
	{
		return ((BattleChara)(nint)((IGameObject)local).Address).ContentId;
	}
}
