using System;

namespace KickChaos;

public class MenuItem
{
	public string Label = string.Empty;

	public Func<string> Value;

	public Action OnEnter;

	public Action<int> OnChange;

	public Func<MenuPage> Submenu;

	public Func<string> Help;

	public bool Info;

	public Func<string> DynamicLabel;

	public string GetLabel()
	{
		return (DynamicLabel == null) ? Label : DynamicLabel();
	}
}
