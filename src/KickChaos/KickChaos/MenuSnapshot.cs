using System.Collections.Generic;

namespace KickChaos;

public class MenuSnapshot
{
	public string Header;

	public string Title;

	public string Footer;

	public string Help;

	public List<string[]> Rows = new List<string[]>();

	public bool MoreAbove;

	public bool MoreBelow;

	public string Typing;
}
