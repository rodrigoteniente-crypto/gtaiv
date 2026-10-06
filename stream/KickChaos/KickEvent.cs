using System;
using System.Collections.Generic;

namespace KickChaos;

public class KickEvent
{
	public KickEventKind Kind;

	public string EventName = string.Empty;

	public string User = string.Empty;

	public string Text = string.Empty;

	public int Count = 1;

	public readonly List<string> Recipients = new List<string>();

	public int Level;

	/// <summary>Color del nombre en el chat de Kick ("#RRGGBB"), si viene.</summary>
	public string Color = string.Empty;

	public string Raw = string.Empty;

	public DateTime ReceivedUtc = DateTime.UtcNow;

	public bool Simulated;
}
