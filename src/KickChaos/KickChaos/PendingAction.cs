using System.Collections.Generic;

namespace KickChaos;

public class PendingAction
{
	public List<ActionStep> Steps;

	public int Multiplier = 1;

	public string User = string.Empty;

	public string Source = string.Empty;

	public bool FromEvent;

	public int Count;

	public override string ToString()
	{
		return string.Join("+", Steps.ConvertAll((ActionStep s) => s.ToString()).ToArray()) + ((Multiplier <= 1) ? string.Empty : (" (x" + Multiplier + ")"));
	}
}
