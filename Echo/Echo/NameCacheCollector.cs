using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Echo.Core;
using Echo.Wire;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Echo;

internal sealed class NameCacheCollector(IFramework framework, IPartyFinderGui partyFinder, IContextMenu contextMenu, IObjectTable objectTable, CaptureEngine nameCacheEngine, Outbox outbox, PluginState state, IPluginLog log) : IDisposable
{
	private readonly ConcurrentQueue<CapturedPlayer> _queue = new ConcurrentQueue<CapturedPlayer>();

	public void Enable()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		partyFinder.ReceiveListing += new PartyFinderListingEventDelegate(OnListing);
		contextMenu.OnMenuOpened += new OnMenuOpenedDelegate(OnMenuOpened);
		framework.Update += new OnUpdateDelegate(OnUpdate);
	}

	public void Dispose()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		partyFinder.ReceiveListing -= new PartyFinderListingEventDelegate(OnListing);
		contextMenu.OnMenuOpened -= new OnMenuOpenedDelegate(OnMenuOpened);
		framework.Update -= new OnUpdateDelegate(OnUpdate);
	}

	private void OnListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (Enabled() && listing.ContentId != 0L)
			{
				_queue.Enqueue(new CapturedPlayer(listing.ContentId, listing.Name.TextValue, listing.HomeWorld.RowId, 0u, 0, 0f, 0f, 0f, 0, 0, null, null, "namecache", 0uL, 0, 0, null, 0uL, 0uL));
			}
		}
		catch (Exception ex)
		{
			log.Verbose(ex, "Echo PF capture failed", Array.Empty<object>());
		}
	}

	private void OnMenuOpened(IMenuOpenedArgs args)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected O, but got Unknown
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			MenuTarget target = ((IMenuArgs)args).Target;
			MenuTargetDefault val = (MenuTargetDefault)(object)((target is MenuTargetDefault) ? target : null);
			if (val == null || val.TargetContentId == 0L || val.TargetName.Length == 0)
			{
				return;
			}
			if (state.Snapshot().ContextMenuLinkEnabled)
			{
				string url = ProfileLink.For(val.TargetHomeWorld.RowId, val.TargetName);
				args.AddMenuItem(new MenuItem
				{
					Name = SeString.op_Implicit("View on EchoVault"),
					OnClicked = delegate
					{
						Util.OpenLink(url);
					}
				});
			}
			if (Enabled())
			{
				_queue.Enqueue(new CapturedPlayer(val.TargetContentId, val.TargetName, val.TargetHomeWorld.RowId, 0u, 0, 0f, 0f, 0f, 0, 0, null, null, "namecache", 0uL, 0, 0, null, 0uL, 0uL));
			}
		}
		catch (Exception ex)
		{
			log.Verbose(ex, "Echo context-menu capture failed", Array.Empty<object>());
		}
	}

	private bool Enabled()
	{
		PluginStateSnapshot pluginStateSnapshot = state.Snapshot();
		if (pluginStateSnapshot.CaptureEnabled && pluginStateSnapshot.NameCacheCaptureEnabled)
		{
			return pluginStateSnapshot.ServerAllowsIngest;
		}
		return false;
	}

	private void OnUpdate(IFramework _)
	{
		try
		{
			if (_queue.IsEmpty)
			{
				return;
			}
			IPlayerCharacter localPlayer = objectTable.LocalPlayer;
			if (localPlayer == null)
			{
				return;
			}
			ulong localContentId = ReadLocalContentId(localPlayer);
			List<CapturedPlayer> list = new List<CapturedPlayer>();
			CapturedPlayer result;
			while (list.Count < 50 && _queue.TryDequeue(out result))
			{
				list.Add(result);
			}
			foreach (Sighting item in nameCacheEngine.Process(list, localContentId, DateTimeOffset.UtcNow))
			{
				outbox.Append(JsonSerializer.Serialize(item, WireJson.Options));
			}
		}
		catch (Exception ex)
		{
			log.Verbose(ex, "Echo namecache drain failed", Array.Empty<object>());
		}
	}

	private static ulong ReadLocalContentId(IPlayerCharacter local)
	{
		return ((BattleChara)(nint)((IGameObject)local).Address).ContentId;
	}
}
