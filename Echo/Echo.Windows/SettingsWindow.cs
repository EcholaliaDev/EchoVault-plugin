using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Echo.Core;
using Echo.Wire;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Echo.Windows;

public sealed class SettingsWindow : Window
{
	private readonly PluginState _state;

	private readonly EchoApiClient _client;

	private readonly IObjectTable _objectTable;

	private readonly IPluginLog _log;

	private string _lodestoneId = "";

	private volatile string _verifyCode = "";

	private volatile string _verifyStatus = "";

	private volatile bool _verifyBusy;

	private volatile string _claimCode = "";

	private long _claimCodeExpiresAtTicks;

	private volatile string _claimStatus = "";

	private volatile bool _claimBusy;

	public SettingsWindow(PluginState state, EchoApiClient client, IObjectTable objectTable, IPluginLog log)
		: base("Echo v0.4.1###EchoSettings")
	{
		_state = state;
		_client = client;
		_objectTable = objectTable;
		_log = log;
		((Window)this).Size = new Vector2(420f, 320f);
		((Window)this).SizeCondition = (ImGuiCond)4;
	}

	public override void Draw()
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		PluginStateSnapshot snap = _state.Snapshot();
		ImU8String val = default(ImU8String);
		((ImU8String)(ref val))._002Ector(9, 1);
		((ImU8String)(ref val)).AppendLiteral("Account: ");
		((ImU8String)(ref val)).AppendFormatted<RegistrationStatus>(snap.Registration);
		ImGui.TextUnformatted(val);
		ImU8String val2 = default(ImU8String);
		((ImU8String)(ref val2))._002Ector(16, 1);
		((ImU8String)(ref val2)).AppendLiteral("Outbox: ");
		((ImU8String)(ref val2)).AppendFormatted<int>(snap.OutboxDepth);
		((ImU8String)(ref val2)).AppendLiteral(" pending");
		ImGui.TextUnformatted(val2);
		DateTimeOffset? lastUploadAt = snap.LastUploadAt;
		if (lastUploadAt.HasValue)
		{
			DateTimeOffset last = lastUploadAt.GetValueOrDefault();
			ImU8String val3 = default(ImU8String);
			((ImU8String)(ref val3))._002Ector(13, 1);
			((ImU8String)(ref val3)).AppendLiteral("Last upload: ");
			((ImU8String)(ref val3)).AppendFormatted<DateTimeOffset>(last.ToLocalTime(), "HH:mm:ss");
			ImGui.TextUnformatted(val3);
		}
		if (!snap.ServerAllowsIngest)
		{
			Vector4 vector = new Vector4(1f, 0.7f, 0.2f, 1f);
			ImGui.TextColored(ref vector, ImU8String.op_Implicit("Uploads disabled by server (update Echo?)."));
		}
		string error = snap.LastError;
		if (error != null)
		{
			Vector4 vector = new Vector4(1f, 0.3f, 0.3f, 1f);
			ImGui.TextColored(ref vector, ImU8String.op_Implicit(error));
		}
		ImGui.Separator();
		bool capture = snap.CaptureEnabled;
		if (ImGui.Checkbox(ImU8String.op_Implicit("Enable capture"), ref capture))
		{
			_state.SetCaptureEnabled(capture);
		}
		bool social = snap.SocialCaptureEnabled;
		if (ImGui.Checkbox(ImU8String.op_Implicit("Capture party/FC/friend lists"), ref social))
		{
			_state.SetSocialCaptureEnabled(social);
		}
		bool nameCache = snap.NameCacheCaptureEnabled;
		if (ImGui.Checkbox(ImU8String.op_Implicit("Capture Party Finder & lookups"), ref nameCache))
		{
			_state.SetNameCacheCaptureEnabled(nameCache);
		}
		ImGui.Separator();
		if (snap.Registration == RegistrationStatus.Verified)
		{
			Vector4 vector = new Vector4(0.4f, 0.9f, 0.4f, 1f);
			ImGui.TextColored(ref vector, ImU8String.op_Implicit("Verified - your uploads go live immediately."));
		}
		else
		{
			ImGui.TextUnformatted(ImU8String.op_Implicit("Verify your character (optional)"));
			ImGui.SameLine();
			HelpMarker("Verification links your character's Lodestone profile to prove ownership. Verified uploads go live immediately instead of waiting for corroboration. Free-trial characters have no Lodestone profile and cannot verify. Your ID is the number in your Lodestone page URL: na.finalfantasyxiv.com/lodestone/character/<YOUR ID>/");
			ImGui.InputText(ImU8String.op_Implicit("Lodestone character ID"), ref _lodestoneId, 20, (ImGuiInputTextFlags)0, (ImGuiInputTextCallbackDelegate)null);
			if (_verifyBusy)
			{
				ImGui.TextUnformatted(ImU8String.op_Implicit("Working..."));
			}
			else
			{
				if (ImGui.Button(ImU8String.op_Implicit("1. Get verification code"), default(Vector2)))
				{
					StartVerifyAsync();
				}
				if (_verifyCode.Length > 0)
				{
					ImU8String val4 = default(ImU8String);
					((ImU8String)(ref val4))._002Ector(39, 1);
					((ImU8String)(ref val4)).AppendLiteral("Code: ");
					((ImU8String)(ref val4)).AppendFormatted<string>(_verifyCode);
					((ImU8String)(ref val4)).AppendLiteral("  (paste into your Lodestone bio)");
					ImGui.TextUnformatted(val4);
					if (ImGui.Button(ImU8String.op_Implicit("2. I saved it - verify now"), default(Vector2)))
					{
						CompleteVerifyAsync();
					}
				}
			}
			if (_verifyStatus.Length > 0)
			{
				ImGui.TextUnformatted(ImU8String.op_Implicit(_verifyStatus));
			}
		}
		ImGui.Separator();
		DrawClaimSection();
	}

	private void DrawClaimSection()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		ImGui.TextUnformatted(ImU8String.op_Implicit("Claim this character on echovault.gg"));
		ImGui.SameLine();
		HelpMarker("Minting a code links the character you are logged into with your echovault.gg login, so you can manage its privacy settings on the site. Enter the code at echovault.gg/me within 10 minutes. Works for free-trial characters too. Minting a new code replaces any earlier one.");
		if (_claimBusy)
		{
			ImGui.TextUnformatted(ImU8String.op_Implicit("Working..."));
		}
		else if (ImGui.Button(ImU8String.op_Implicit("Get claim code"), default(Vector2)))
		{
			StartClaimAsync();
		}
		DateTimeOffset expiresAt = new DateTimeOffset(Interlocked.Read(in _claimCodeExpiresAtTicks), TimeSpan.Zero);
		if (_claimCode.Length > 0 && expiresAt > DateTimeOffset.UtcNow)
		{
			string code = _claimCode;
			ImGui.SetNextItemWidth(120f);
			ImGui.InputText(ImU8String.op_Implicit("##echoClaimCode"), ref code, 16, (ImGuiInputTextFlags)16384, (ImGuiInputTextCallbackDelegate)null);
			ImGui.SameLine();
			if (ImGui.Button(ImU8String.op_Implicit("Copy"), default(Vector2)))
			{
				ImGui.SetClipboardText(ImU8String.op_Implicit(_claimCode));
			}
			TimeSpan left = expiresAt - DateTimeOffset.UtcNow;
			ImU8String val = default(ImU8String);
			((ImU8String)(ref val))._002Ector(42, 1);
			((ImU8String)(ref val)).AppendLiteral("Enter it at echovault.gg/me - expires in ");
			((ImU8String)(ref val)).AppendFormatted<TimeSpan>(left, "m\\:ss");
			((ImU8String)(ref val)).AppendLiteral(".");
			ImGui.TextUnformatted(val);
		}
		if (_claimStatus.Length > 0)
		{
			ImGui.TextUnformatted(ImU8String.op_Implicit(_claimStatus));
		}
	}

	private async Task StartClaimAsync()
	{
		_claimBusy = true;
		try
		{
			IPlayerCharacter local = _objectTable.LocalPlayer;
			if (local == null)
			{
				_claimStatus = "Log in to a character first.";
				return;
			}
			ulong contentId = ReadContentId(local);
			string name = ((IGameObject)local).Name.TextValue;
			uint world = local.HomeWorld.RowId;
			if (contentId == 0L)
			{
				_claimStatus = "Could not read the character id - try again in a moment.";
				return;
			}
			LinkStartResult result = await _client.LinkStartAsync(new LinkStartRequest(2, contentId, name, world), CancellationToken.None);
			LinkStartResponse resp = result.Response;
			if ((object)resp != null)
			{
				_claimCode = resp.Code;
				Interlocked.Exchange(ref _claimCodeExpiresAtTicks, resp.ExpiresAt.UtcTicks);
				_claimStatus = "";
			}
			else
			{
				_claimCode = "";
				_claimStatus = Capitalize(LinkClaimMessages.Describe(result.Error));
			}
		}
		catch (Exception ex)
		{
			_log.Error(ex, "claim code mint failed", Array.Empty<object>());
			_claimStatus = "Error getting a claim code.";
		}
		finally
		{
			_claimBusy = false;
		}
	}

	private static string Capitalize(string s)
	{
		if (s.Length <= 0)
		{
			return s;
		}
		return char.ToUpperInvariant(s[0]) + s.Substring(1);
	}

	private static string VerifyReasonText(string? reason)
	{
		return reason switch
		{
			"lodestone_unavailable" => "the Lodestone is busy - try again in a minute (your code is still valid).", 
			"code_not_found" => "code not found in your Lodestone bio yet - save the profile, wait a moment, then retry.", 
			"character_mismatch" => "that Lodestone character doesn't match who you're logged in as.", 
			"not_found" => "no Lodestone profile with that ID.", 
			"level_too_low" => "this character doesn't meet the level requirement yet.", 
			"character_too_new" => "this character is too new to verify yet.", 
			"banned" => "this account is disabled.", 
			"expired" => "the code expired - get a new one.", 
			null => "no response from the server.", 
			_ => reason.Replace('_', ' ') + ".", 
		};
	}

	private static void HelpMarker(string text)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		ImGui.TextDisabled(ImU8String.op_Implicit("(?)"));
		if (ImGui.IsItemHovered())
		{
			ImGui.BeginTooltip();
			ImGui.PushTextWrapPos(ImGui.GetFontSize() * 24f);
			ImGui.TextUnformatted(ImU8String.op_Implicit(text));
			ImGui.PopTextWrapPos();
			ImGui.EndTooltip();
		}
	}

	private static ulong ReadContentId(IPlayerCharacter local)
	{
		return ((BattleChara)(nint)((IGameObject)local).Address).ContentId;
	}

	private async Task StartVerifyAsync()
	{
		_verifyBusy = true;
		try
		{
			IPlayerCharacter local = _objectTable.LocalPlayer;
			if (local == null)
			{
				_verifyStatus = "Log in to a character first.";
				return;
			}
			ulong localContentId = ReadContentId(local);
			if (localContentId == 0L)
			{
				_verifyStatus = "Log in to a character first.";
				return;
			}
			string name = ((IGameObject)local).Name.TextValue;
			World value = local.HomeWorld.Value;
			ReadOnlySeString name2 = ((World)(ref value)).Name;
			string homeWorldName = ((ReadOnlySeString)(ref name2)).ExtractText();
			string lodestoneId = _lodestoneId.Trim();
			if (lodestoneId.Length == 0 || !lodestoneId.All(char.IsAsciiDigit))
			{
				_verifyStatus = "Enter your numeric Lodestone character ID first (the number in your Lodestone profile URL). Digits only.";
				return;
			}
			VerifyStartResponse response = await _client.VerifyStartAsync(new VerifyStartRequest(2, lodestoneId, name, homeWorldName, localContentId), CancellationToken.None);
			if ((object)response == null)
			{
				_verifyStatus = "Could not start verification. Check the ID is yours and unclaimed - and note free-trial characters cannot verify (no Lodestone profile).";
				return;
			}
			_verifyCode = response.Code;
			_verifyStatus = "";
		}
		catch (Exception ex)
		{
			_log.Error(ex, "verify start failed", Array.Empty<object>());
			_verifyStatus = "Error starting verification.";
		}
		finally
		{
			_verifyBusy = false;
		}
	}

	private async Task CompleteVerifyAsync()
	{
		_verifyBusy = true;
		try
		{
			VerifyCompleteResponse response = await _client.VerifyCompleteAsync(CancellationToken.None);
			if ((object)response != null && response.Verified)
			{
				_state.SetRegistration(RegistrationStatus.Verified);
				_verifyStatus = "Verified! Your uploads go live immediately now.";
				_verifyCode = "";
			}
			else
			{
				_verifyStatus = "Not verified: " + VerifyReasonText(response?.Reason);
			}
		}
		catch (Exception ex)
		{
			_log.Error(ex, "verify complete failed", Array.Empty<object>());
			_verifyStatus = "Error completing verification.";
		}
		finally
		{
			_verifyBusy = false;
		}
	}
}
