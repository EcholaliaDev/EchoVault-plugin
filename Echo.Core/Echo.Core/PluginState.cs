using System;
using Echo.Wire;

namespace Echo.Core;

public sealed class PluginState
{
	private readonly object _lock = new object();

	private PluginStateSnapshot _snapshot = new PluginStateSnapshot(RegistrationStatus.Unregistered, 0, null, null, CaptureEnabled: true, ServerAllowsIngest: true);

	public PluginStateSnapshot Snapshot()
	{
		lock (_lock)
		{
			return _snapshot;
		}
	}

	public void SetRegistration(RegistrationStatus v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				Registration = v
			};
		}
	}

	public void SetOutboxDepth(int v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				OutboxDepth = v
			};
		}
	}

	public void SetLastUpload(DateTimeOffset v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				LastUploadAt = v
			};
		}
	}

	public void SetError(string? v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				LastError = v
			};
		}
	}

	public void SetCaptureEnabled(bool v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				CaptureEnabled = v
			};
		}
	}

	public void SetServerAllowsIngest(bool v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				ServerAllowsIngest = v
			};
		}
	}

	public void SetReporter(ReporterSelf v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				Reporter = v
			};
		}
	}

	public void SetSocialCaptureEnabled(bool v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				SocialCaptureEnabled = v
			};
		}
	}

	public void SetNameCacheCaptureEnabled(bool v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				NameCacheCaptureEnabled = v
			};
		}
	}

	public void SetContextMenuLinkEnabled(bool v)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with
			{
				ContextMenuLinkEnabled = v
			};
		}
	}
}
