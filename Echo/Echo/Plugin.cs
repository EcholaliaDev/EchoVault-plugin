using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Echo.Core;
using Echo.Windows;
using Echo.Wire;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace Echo;

public sealed class Plugin : IDalamudPlugin, IDisposable
{
	private const string Command = "/echovault";

	private const string ServerBaseUrl = "https://echovault.gg";

	internal const string PluginVersion = "0.5.0";

	private readonly ICommandManager _commands;

	private readonly WindowSystem _windows = new WindowSystem("Echo");

	private readonly SettingsWindow _settingsWindow;

	private readonly ObjectTableSweeper _sweeper;

	private readonly SocialCollector _social;

	private readonly NameCacheCollector _nameCache;

	private readonly HttpClient _http;

	private readonly CancellationTokenSource _cts = new CancellationTokenSource();

	private readonly Task _drainTask;

	private readonly IDalamudPluginInterface _pluginInterface;

	private readonly IChatGui _chat;

	private readonly IObjectTable _objectTable;

	private readonly EchoApiClient _client;

	internal PluginState State { get; } = new PluginState();

	internal CaptureEngine Engine { get; } = new CaptureEngine();

	internal CaptureEngine SocialEngine { get; } = new CaptureEngine();

	internal CaptureEngine NameCacheEngine { get; } = new CaptureEngine();

	internal Outbox Outbox { get; }

	public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IFramework framework, IObjectTable objectTable, IClientState clientState, IPartyList partyList, IDataManager dataManager, IPartyFinderGui partyFinderGui, IContextMenu contextMenu, IChatGui chatGui, IPluginLog log)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected O, but got Unknown
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Expected O, but got Unknown
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d0: Expected O, but got Unknown
		_pluginInterface = pluginInterface;
		_commands = commands;
		_chat = chatGui;
		_objectTable = objectTable;
		string pluginConfigDirectory = pluginInterface.GetPluginConfigDirectory();
		Outbox = new Outbox(Path.Combine(pluginConfigDirectory, "outbox.jsonl"), 10485760L);
		KeyStore keyStore = new KeyStore(Path.Combine(pluginConfigDirectory, "keys.bin"), new DpapiKeyProtector());
		_http = new HttpClient
		{
			BaseAddress = new Uri("https://echovault.gg")
		};
		_client = new EchoApiClient(_http, keyStore);
		DrainLoop drain = new DrainLoop(Outbox, _client, new BackoffPolicy(), State);
		StoredCredentials storedCredentials = keyStore.Load();
		if ((object)storedCredentials != null)
		{
			PluginState state = State;
			bool flag;
			switch (storedCredentials.Tier)
			{
			case "standard":
			case "trusted":
			case "verified":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			state.SetRegistration((!flag) ? RegistrationStatus.Registered : RegistrationStatus.Verified);
		}
		_sweeper = new ObjectTableSweeper(framework, objectTable, clientState, Engine, Outbox, State, log);
		_sweeper.Enable();
		_social = new SocialCollector(framework, partyList, objectTable, dataManager, SocialEngine, Outbox, State, log);
		_social.Enable();
		_nameCache = new NameCacheCollector(framework, partyFinderGui, contextMenu, objectTable, NameCacheEngine, Outbox, State, log);
		_nameCache.Enable();
		drain.OnConfig = delegate(ConfigResponse config)
		{
			TimeSpan cadence = TimeSpan.FromSeconds(config.CaptureCadenceSeconds);
			TimeSpan floor = TimeSpan.FromSeconds(config.MinEmitIntervalSeconds);
			Engine.SetCadence(cadence);
			Engine.SetFloor(floor);
			SocialEngine.SetCadence(cadence);
			SocialEngine.SetFloor(floor);
			NameCacheEngine.SetCadence(cadence);
			NameCacheEngine.SetFloor(floor);
		};
		_drainTask = Task.Run(async delegate
		{
			await drain.RunAsync("0.5.0", _cts.Token);
		}, _cts.Token);
		_settingsWindow = new SettingsWindow(State, _client, objectTable, log);
		_windows.AddWindow((IWindow)(object)_settingsWindow);
		pluginInterface.UiBuilder.Draw += _windows.Draw;
		pluginInterface.UiBuilder.OpenConfigUi += ToggleWindow;
		_commands.AddHandler("/echovault", new CommandInfo(new HandlerDelegate(OnCommand))
		{
			HelpMessage = "Open the Echo settings window. \"/echovault link\" mints a site claim code."
		});
		log.Information("Echo loaded.", Array.Empty<object>());
	}

	private void ToggleWindow()
	{
		((Window)_settingsWindow).IsOpen = !((Window)_settingsWindow).IsOpen;
	}

	private void OnCommand(string command, string args)
	{
		if (string.Equals(args.Trim(), "link", StringComparison.OrdinalIgnoreCase))
		{
			LinkAsync();
		}
		else
		{
			ToggleWindow();
		}
	}

	private static ulong ReadContentId(IPlayerCharacter local)
	{
		return ((BattleChara)(nint)((IGameObject)local).Address).ContentId;
	}

	private async Task LinkAsync()
	{
		try
		{
			IPlayerCharacter localPlayer = _objectTable.LocalPlayer;
			if (localPlayer == null)
			{
				_chat.Print("Echo: log in to a character first.", (string)null, (ushort?)null);
				return;
			}
			ulong num = ReadContentId(localPlayer);
			string textValue = ((IGameObject)localPlayer).Name.TextValue;
			uint rowId = localPlayer.HomeWorld.RowId;
			if (num == 0L)
			{
				_chat.Print("Echo: could not read the character id - try again in a moment.", (string)null, (ushort?)null);
				return;
			}
			LinkStartResult linkStartResult = await _client.LinkStartAsync(new LinkStartRequest(2, num, textValue, rowId), _cts.Token);
			IChatGui chat = _chat;
			LinkStartResponse response = linkStartResult.Response;
			chat.Print(((object)response != null) ? ("Echo: claim code " + response.Code + " - enter it at echovault.gg/me within 10 minutes. The /echovault window shows it with a copy button.") : ("Echo: " + LinkClaimMessages.Describe(linkStartResult.Error)), (string)null, (ushort?)null);
		}
		catch (Exception)
		{
			try
			{
				_chat.Print("Echo: link failed - unexpected error.", (string)null, (ushort?)null);
			}
			catch
			{
			}
		}
	}

	public void Dispose()
	{
		_commands.RemoveHandler("/echovault");
		_pluginInterface.UiBuilder.Draw -= _windows.Draw;
		_pluginInterface.UiBuilder.OpenConfigUi -= ToggleWindow;
		_windows.RemoveAllWindows();
		_sweeper.Dispose();
		_social.Dispose();
		_nameCache.Dispose();
		_cts.Cancel();
		try
		{
			_drainTask.Wait(TimeSpan.FromSeconds(5L));
		}
		catch (AggregateException)
		{
		}
		_http.Dispose();
		_cts.Dispose();
	}
}
