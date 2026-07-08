using System;
using System.Collections.Generic;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Echo.Core;
using Echo.Wire;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Echo;

internal sealed class ObjectTableSweeper(IFramework framework, IObjectTable objectTable, IClientState clientState, CaptureEngine captureEngine, Outbox outbox, PluginState state, IPluginLog log) : IDisposable
{
	private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5L);

	private static readonly TimeSpan SpawnDiffInterval = TimeSpan.FromSeconds(1L);

	private DateTime _lastSweep = DateTime.MinValue;

	private DateTime _lastDiff = DateTime.MinValue;

	private readonly HashSet<ulong> _present = new HashSet<ulong>();

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

	private unsafe void OnUpdate(IFramework _)
	{
		try
		{
			DateTime utcNow = DateTime.UtcNow;
			bool flag = utcNow - _lastSweep >= SweepInterval;
			bool flag2 = utcNow - _lastDiff >= SpawnDiffInterval;
			if (!flag && !flag2)
			{
				return;
			}
			PluginStateSnapshot pluginStateSnapshot = state.Snapshot();
			if (!pluginStateSnapshot.CaptureEnabled || !pluginStateSnapshot.ServerAllowsIngest)
			{
				return;
			}
			IPlayerCharacter localPlayer = objectTable.LocalPlayer;
			if (localPlayer == null)
			{
				return;
			}
			ushort num = (ushort)clientState.TerritoryType;
			state.SetReporter(new ReporterSelf(num, ((IGameObject)localPlayer).Position.X, ((IGameObject)localPlayer).Position.Y, ((IGameObject)localPlayer).Position.Z));
			BattleChara* address = (BattleChara*)((IGameObject)localPlayer).Address;
			ulong contentId = ((BattleChara)address).ContentId;
			HashSet<ulong> hashSet = new HashSet<ulong>();
			List<CapturedPlayer> list = (flag ? new List<CapturedPlayer>() : null);
			List<CapturedPlayer> list2 = (flag2 ? new List<CapturedPlayer>() : null);
			foreach (IBattleChara playerObject in objectTable.PlayerObjects)
			{
				IPlayerCharacter val = (IPlayerCharacter)(object)((playerObject is IPlayerCharacter) ? playerObject : null);
				if (val == null)
				{
					continue;
				}
				BattleChara* address2 = (BattleChara*)((IGameObject)val).Address;
				ulong contentId2 = ((BattleChara)address2).ContentId;
				hashSet.Add(contentId2);
				if (contentId2 != contentId)
				{
					if (flag2 && !_present.Contains(contentId2))
					{
						list2.Add(Capture(val, address2, num, "spawn"));
					}
					if (flag)
					{
						list.Add(Capture(val, address2, num, "sweep"));
					}
				}
			}
			if (flag2)
			{
				_lastDiff = utcNow;
				_present.Clear();
				_present.UnionWith(hashSet);
				Emit(captureEngine, list2, contentId);
			}
			if (flag)
			{
				_lastSweep = utcNow;
				Emit(captureEngine, list, contentId);
				state.SetOutboxDepth(outbox.Count());
			}
		}
		catch (Exception ex)
		{
			log.Verbose(ex, "Echo sweep failed", Array.Empty<object>());
		}
	}

	private void Emit(CaptureEngine engine, List<CapturedPlayer> captured, ulong localContentId)
	{
		if (captured.Count == 0)
		{
			return;
		}
		foreach (Sighting item in engine.Process(captured, localContentId, DateTimeOffset.UtcNow))
		{
			outbox.Append(JsonSerializer.Serialize(item, WireJson.Options));
		}
	}

	private unsafe static CapturedPlayer Capture(IPlayerCharacter pc, BattleChara* chara, ushort territory, string source)
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		List<EquipSlot> list = new List<EquipSlot>(10);
		Span<EquipmentModelId> equipmentModelIds = ((DrawDataContainer)(&((BattleChara)chara).DrawData)).EquipmentModelIds;
		for (int i = 0; i < equipmentModelIds.Length; i++)
		{
			ref EquipmentModelId reference = ref equipmentModelIds[i];
			list.Add(new EquipSlot(reference.Id, reference.Variant, reference.Stain0, reference.Stain1));
		}
		ulong contentId = ((BattleChara)chara).ContentId;
		string textValue = ((IGameObject)pc).Name.TextValue;
		uint rowId = pc.HomeWorld.RowId;
		uint rowId2 = pc.CurrentWorld.RowId;
		float x = ((IGameObject)pc).Position.X;
		float y = ((IGameObject)pc).Position.Y;
		float z = ((IGameObject)pc).Position.Z;
		byte jobId = (byte)((ICharacter)pc).ClassJob.RowId;
		byte level = ((ICharacter)pc).Level;
		string textValue2 = ((ICharacter)pc).CompanyTag.TextValue;
		string fcTag = ((textValue2 != null && textValue2.Length > 0) ? textValue2 : null);
		byte[] customize = ((((ICharacter)pc).Customize.Length > 0) ? ((ICharacter)pc).Customize.ToArray() : null);
		ulong accountId = ((BattleChara)chara).AccountId;
		ushort titleId = ((BattleChara)chara).TitleId;
		byte battalion = ((BattleChara)chara).Battalion;
		ulong value = ((DrawDataContainer)(&((BattleChara)chara).DrawData)).Weapon((WeaponSlot)0).ModelId.Value;
		ulong value2 = ((DrawDataContainer)(&((BattleChara)chara).DrawData)).Weapon((WeaponSlot)1).ModelId.Value;
		World? valueNullable = pc.HomeWorld.ValueNullable;
		object obj;
		if (!valueNullable.HasValue)
		{
			obj = null;
		}
		else
		{
			World valueOrDefault = valueNullable.GetValueOrDefault();
			ReadOnlySeString name = ((World)(ref valueOrDefault)).Name;
			obj = ((ReadOnlySeString)(ref name)).ExtractText();
		}
		if (obj == null)
		{
			obj = string.Empty;
		}
		return new CapturedPlayer(contentId, textValue, rowId, rowId2, territory, x, y, z, jobId, level, fcTag, customize, source, accountId, titleId, battalion, list, value, value2, (string?)obj);
	}
}
