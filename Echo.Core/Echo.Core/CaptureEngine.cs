using System;
using System.Collections.Generic;
using System.Linq;
using Echo.Wire;

namespace Echo.Core;

public sealed class CaptureEngine(TimeSpan? cadence = null)
{
	private sealed record Seen(DateTimeOffset EmittedAt, DateTimeOffset LastSeenAt, int Hash);

	private readonly Dictionary<ulong, Seen> _seen = new Dictionary<ulong, Seen>();

	private static readonly TimeSpan PruneAfter = TimeSpan.FromHours(1.0);

	private TimeSpan _cadence = cadence ?? TimeSpan.FromSeconds(30.0);

	private TimeSpan _floor = TimeSpan.FromSeconds(10.0);

	private DateTimeOffset? _startedAt;

	public void SetCadence(TimeSpan value)
	{
		_cadence = value;
	}

	public void SetFloor(TimeSpan value)
	{
		_floor = value;
	}

	public IReadOnlyList<Sighting> Process(IEnumerable<CapturedPlayer> players, ulong localContentId, DateTimeOffset nowUtc)
	{
		DateTimeOffset valueOrDefault = _startedAt.GetValueOrDefault();
		if (!_startedAt.HasValue)
		{
			valueOrDefault = nowUtc;
			_startedAt = valueOrDefault;
		}
		bool coldStart = nowUtc - _startedAt.Value < _floor;
		List<Sighting> result = new List<Sighting>();
		foreach (CapturedPlayer p in players)
		{
			if (p.ContentId < 18014398509481984L || p.ContentId == localContentId || !Protocol.IsValidCharacterName(p.Name))
			{
				continue;
			}
			int hash = HashOf(p);
			Seen seen;
			if (coldStart)
			{
				if (_seen.TryGetValue(p.ContentId, out Seen baselined))
				{
					_seen[p.ContentId] = baselined with
					{
						LastSeenAt = nowUtc
					};
				}
				else
				{
					_seen[p.ContentId] = new Seen(nowUtc, nowUtc, hash);
				}
			}
			else if (_seen.TryGetValue(p.ContentId, out seen) && (nowUtc - seen.EmittedAt < _floor || (seen.Hash == hash && nowUtc - seen.EmittedAt < _cadence)))
			{
				_seen[p.ContentId] = seen with
				{
					LastSeenAt = nowUtc
				};
			}
			else
			{
				_seen[p.ContentId] = new Seen(nowUtc, nowUtc, hash);
				result.Add(new Sighting(p.ContentId, p.Name, p.HomeWorldId, p.CurrentWorldId, p.TerritoryId, p.X, p.Y, p.Z, p.JobId, p.Level, p.FcTag, (p.Customize == null) ? null : Convert.ToBase64String(p.Customize), nowUtc, p.Source, p.AccountId, p.TitleId, p.GrandCompany, p.Equipment, p.MainhandModel, p.OffhandModel, p.HomeWorldName));
			}
		}
		foreach (ulong stale in (from kv in _seen
			where nowUtc - kv.Value.LastSeenAt > PruneAfter
			select kv.Key).ToList())
		{
			_seen.Remove(stale);
		}
		return result;
	}

	private static int HashOf(CapturedPlayer p)
	{
		HashCode hash = default(HashCode);
		hash.Add(p.Name);
		hash.Add(p.HomeWorldId);
		hash.Add(p.CurrentWorldId);
		hash.Add(p.TerritoryId);
		hash.Add(p.JobId);
		hash.Add(p.Level);
		hash.Add(p.FcTag);
		if (p.Customize != null)
		{
			hash.AddBytes(p.Customize);
		}
		hash.Add(p.TitleId);
		hash.Add(p.GrandCompany);
		hash.Add(p.MainhandModel);
		hash.Add(p.OffhandModel);
		if (p.Equipment != null)
		{
			foreach (EquipSlot e in p.Equipment)
			{
				hash.Add(e);
			}
		}
		return hash.ToHashCode();
	}
}
