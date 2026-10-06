namespace KickChaos;

public class QueuedAction
{
	public string Name;

	public string User = string.Empty;

	public string Source = string.Empty;

	public bool FromEvent;

	public int Count;

	public bool ChainNext;
}
