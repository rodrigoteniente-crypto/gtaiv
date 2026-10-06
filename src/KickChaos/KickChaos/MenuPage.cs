using System;
using System.Collections.Generic;

namespace KickChaos;

public class MenuPage
{
	public string Title;

	public Func<List<MenuItem>> Build;

	public List<MenuItem> Items = new List<MenuItem>();

	public int Selected;

	public int Scroll;

	public MenuPage(string title, Func<List<MenuItem>> build)
	{
		Title = title;
		Build = build;
	}

	public void Refresh()
	{
		Items = Build() ?? new List<MenuItem>();
		if (Selected >= Items.Count)
		{
			Selected = Math.Max(0, Items.Count - 1);
		}
		if (Items.Count > 0 && Items[Selected].Info)
		{
			MoveToSelectable(1);
		}
	}

	public void MoveToSelectable(int dir)
	{
		if (Items.Count == 0)
		{
			return;
		}
		for (int i = 0; i < Items.Count; i++)
		{
			if (!Items[Selected].Info)
			{
				break;
			}
			Selected = (Selected + dir + Items.Count) % Items.Count;
		}
	}
}
