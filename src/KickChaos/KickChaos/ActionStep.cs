namespace KickChaos;

public class ActionStep
{
	public string Name;

	public int Repeat = 1;

	public override string ToString()
	{
		return (Repeat <= 1) ? Name : (Name + "*" + Repeat);
	}
}
