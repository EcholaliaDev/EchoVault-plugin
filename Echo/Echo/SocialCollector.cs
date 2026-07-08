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
			PluginStateSnapshot pluginStateSnapshot = state.Snapshot();
			if (!pluginStateSnapshot.CaptureEnabled || !pluginStateSnapshot.SocialCaptureEnabled || !pluginStateSnapshot.ServerAllowsIngest)
			{
				return;
			}
			IPlayerCharacter localPlayer = objectTable.LocalPlayer;
			if (localPlayer == null)
			{
				return;
			}
			ulong localContentId = ReadLocalContentId(localPlayer);
			DateTime utcNow = DateTime.UtcNow;
			List<CapturedPlayer> list = new List<CapturedPlayer>();
			if (utcNow - _lastParty >= PartyInterval)
			{
				_lastParty = utcNow;
				InfoProxyCrossRealm* ptr = InfoProxyCrossRealm.Instance();
				if (ptr != null && ((InfoProxyCrossRealm)ptr).IsCrossRealm && ((InfoProxyCrossRealm)ptr).GroupCount > 0)
				{
					Span<CrossRealmGroup> crossRealmGroups = ((InfoProxyCrossRealm)ptr).CrossRealmGroups;
					Span<CrossRealmGroup> span = crossRealmGroups[..Math.Min(((InfoProxyCrossRealm)ptr).GroupCount, crossRealmGroups.Length)];
					for (int i = 0; i < span.Length; i++)
					{
						ref CrossRealmGroup reference = ref span[i];
						CrossRealmGroup val = reference;
						Span<CrossRealmMember> groupMembers = ((CrossRealmGroup)(ref val)).GroupMembers;
						Span<CrossRealmMember> span2 = groupMembers[..Math.Min(reference.GroupMemberCount, groupMembers.Length)];
						for (int j = 0; j < span2.Length; j++)
						{
							ref CrossRealmMember reference2 = ref span2[j];
							ulong contentId = reference2.ContentId;
							CrossRealmMember val2 = reference2;
							CapturedPlayer capturedPlayer = CrossRealmCapture.TryMap(contentId, ((CrossRealmMember)(ref val2)).NameString, reference2.HomeWorld, reference2.CurrentWorld, reference2.ClassJobId, reference2.Level, (reference2.HomeWorld > 0) ? WorldName((uint)reference2.HomeWorld) : null);
							if ((object)capturedPlayer != null)
							{
								list.Add(capturedPlayer);
							}
						}
					}
				}
				else
				{
					foreach (IPartyMember item in (IEnumerable<IPartyMember>)partyList)
					{
						ulong contentId2 = item.ContentId;
						uint rowId = item.World.RowId;
						if (contentId2 != 0L && rowId != 0)
						{
							list.Add(new CapturedPlayer(contentId2, item.Name.TextValue, rowId, rowId, 0, 0f, 0f, 0f, (byte)item.ClassJob.RowId, item.Level, null, null, "social", 0uL, 0, 0, null, 0uL, 0uL, WorldName(rowId)));
						}
					}
				}
			}
			if (utcNow - _lastRoster >= RosterInterval)
			{
				_lastRoster = utcNow;
				CollectProxy((InfoProxyId)15, list);
				CollectProxy((InfoProxyId)6, list);
				CollectProxy((InfoProxyId)4, list);
				CollectProxy((InfoProxyId)31, list);
			}
			if (list.Count == 0)
			{
				return;
			}
			foreach (Sighting item2 in socialEngine.Process(list, localContentId, DateTimeOffset.UtcNow))
			{
				outbox.Append(JsonSerializer.Serialize(item2, WireJson.Options));
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
		InfoModule* ptr = InfoModule.Instance();
		if (ptr == null)
		{
			return;
		}
		InfoProxyCommonList* infoProxyById = (InfoProxyCommonList*)((InfoModule)ptr).GetInfoProxyById(id);
		if (infoProxyById == null)
		{
			return;
		}
		ReadOnlySpan<CharacterData> charDataSpan = ((InfoProxyCommonList)infoProxyById).CharDataSpan;
		for (int i = 0; i < charDataSpan.Length; i++)
		{
			ref readonly CharacterData reference = ref charDataSpan[i];
			ulong contentId = reference.ContentId;
			uint homeWorld = reference.HomeWorld;
			if (contentId != 0L && homeWorld != 0)
			{
				CharacterData val = reference;
				captured.Add(new CapturedPlayer(contentId, ((CharacterData)(ref val)).NameString, homeWorld, homeWorld, 0, 0f, 0f, 0f, reference.Job, 0, null, null, "social", reference.AccountId, 0, 0, null, 0uL, 0uL, WorldName(homeWorld)));
			}
		}
	}

	private static ulong ReadLocalContentId(IPlayerCharacter local)
	{
		return ((BattleChara)(nint)((IGameObject)local).Address).ContentId;
	}
}
