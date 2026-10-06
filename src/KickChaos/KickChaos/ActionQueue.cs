using System;
using System.Collections.Generic;

namespace KickChaos;

public class ActionQueue
{
	private readonly LinkedList<QueuedAction> q = new LinkedList<QueuedAction>();

	private double nextAt;

	public const double ChainGap = 0.45;

	public int Count => q.Count;

	public void Clear()
	{
		q.Clear();
		nextAt = 0;
	}

	public int Enqueue(PendingAction pa, Config cfg, Func<string, string> canonical, List<string> unknown)
	{
		if (pa?.Steps == null) return 0;
		int capacity = Math.Max(1, cfg.MaxQueue);
		List<QueuedAction> list = new List<QueuedAction>();
		foreach (ActionStep step in pa.Steps)
		{
			string text = canonical(step.Name);
			if (text == null)
			{
				unknown?.Add(step.Name);
			}
			else if (!(text == "Nada"))
			{
				int num = (text.StartsWith("Npc", StringComparison.Ordinal) ? 1 : Math.Max(1, pa.Multiplier));
				int num2 = (int)Math.Min((long)Math.Max(1, step.Repeat) * num, Math.Max(1, cfg.MaxRepeat));
				if (list.Count + num2 > capacity) return 0;
				for (int i = 0; i < num2; i++)
				{
					list.Add(new QueuedAction
					{
						Name = text,
						User = pa.User,
						Source = pa.Source,
						FromEvent = pa.FromEvent,
						Count = pa.Count,
						ChainNext = true
					});
				}
			}
		}
		if (list.Count == 0)
		{
			return 0;
		}
		list[list.Count - 1].ChainNext = false;
		if (!pa.FromEvent && q.Count + list.Count > capacity)
		{
			return 0;
		}
		if (pa.FromEvent)
		{
			int replaceable = 0;
			foreach (QueuedAction queued in q) if (!queued.FromEvent) replaceable++;
			if (q.Count - replaceable + list.Count > capacity) return 0;
			LinkedListNode<QueuedAction> linkedListNode = q.First;
			while (q.Count + list.Count > capacity && linkedListNode != null)
			{
				LinkedListNode<QueuedAction> next = linkedListNode.Next;
				if (!linkedListNode.Value.FromEvent)
				{
					q.Remove(linkedListNode);
				}
				linkedListNode = next;
			}
			if (q.Count + list.Count > capacity) return 0;
		}
		foreach (QueuedAction item in list)
		{
			q.AddLast(item);
		}
		return list.Count;
	}

	public QueuedAction TryDequeue(double now, double spacing)
	{
		if (q.Count == 0 || now < nextAt)
		{
			return null;
		}
		QueuedAction value = q.First.Value;
		q.RemoveFirst();
		nextAt = now + ((!value.ChainNext) ? Math.Max(0, spacing) : ChainGap);
		return value;
	}
}
