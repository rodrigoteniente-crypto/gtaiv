using System;
using System.Collections.Generic;

namespace KickChaos;

// Keeps a manually selected NPC separate from the director's timed/weighted
// camera picks. The selector receives the previous handle so repeated presses
// cycle in a stable order, including when the previous ped has disappeared.
public sealed class NpcCameraLock
{
	public int Ped { get; private set; }

	public bool Active => Ped != 0;

	public bool ReturningToLoop { get; private set; }

	public bool AllowsAutomaticFollow => !Active && !ReturningToLoop;

	public int Cycle(int previous, Func<int, int> next, Func<int, bool> alive)
	{
		int candidate = next(previous);
		// Revalidate because a ped can die between enumeration and selection.
		// Skip stale candidates without looping if all handles disappeared.
		var visited = new HashSet<int>();
		while (candidate != 0 && visited.Count < 128 && visited.Add(candidate))
		{
			if (alive(candidate))
			{
				Ped = candidate;
				ReturningToLoop = false;
				return Ped;
			}
			candidate = next(candidate);
		}
		Ped = 0;
		return 0;
	}

	public void Release()
	{
		Ped = 0;
		ReturningToLoop = false;
	}

	public void ReturnToLoop()
	{
		Ped = 0;
		ReturningToLoop = true;
	}

	public void CameraApplied(bool isFollow)
	{
		// Selecting a pending shot is too early: spawns can arrive while the
		// screen fades. Hold suppression until a normal shot is installed.
		if (!isFollow) ReturningToLoop = false;
	}
}
