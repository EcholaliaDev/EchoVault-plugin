using System;

namespace Echo.Core;

public sealed class BackoffPolicy
{
	private static readonly TimeSpan[] Ladder = new TimeSpan[4]
	{
		TimeSpan.FromMinutes(1.0),
		TimeSpan.FromMinutes(5.0),
		TimeSpan.FromMinutes(15.0),
		TimeSpan.FromMinutes(60.0)
	};

	private int _failures;

	public DateTimeOffset? NextAttemptAt { get; private set; }

	public void RecordFailure(DateTimeOffset nowUtc)
	{
		TimeSpan step = Ladder[Math.Min(_failures, Ladder.Length - 1)];
		_failures++;
		NextAttemptAt = nowUtc + step;
	}

	public void RecordSuccess()
	{
		_failures = 0;
		NextAttemptAt = null;
	}

	public bool ShouldAttempt(DateTimeOffset nowUtc)
	{
		if (NextAttemptAt.HasValue)
		{
			DateTimeOffset? nextAttemptAt = NextAttemptAt;
			return nowUtc >= nextAttemptAt;
		}
		return true;
	}
}
