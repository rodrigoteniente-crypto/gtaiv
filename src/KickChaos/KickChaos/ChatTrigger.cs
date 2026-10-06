using System.Collections.Generic;

namespace KickChaos;

public class ChatTrigger
{
	public string RawKey;

	public string Keyword;

	public bool IsCommand;

	public List<ActionStep> Steps;

	public string ActionText;

	public int RequiredLevel;

	public float CooldownSeconds = -1f;
}
