using System;
using System.Numerics;

namespace KickChaos;

public static class MathX
{
	public static readonly Random Rng = new Random();

	public static bool IsFinite(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value);
	}

	public static bool IsFinite(Vector3 value)
	{
		return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
	}

	public static float Clamp(float value, float min, float max, float fallback)
	{
		if (!IsFinite(value))
		{
			value = fallback;
		}
		return Math.Max(min, Math.Min(max, value));
	}

	public static float Rand(float min, float max)
	{
		return min + (float)Rng.NextDouble() * (max - min);
	}

	public static Vector3 Forward(Vector3 rot)
	{
		double num = (double)rot.X * Math.PI / 180.0;
		double num2 = (double)rot.Z * Math.PI / 180.0;
		return new Vector3((float)((0.0 - Math.Sin(num2)) * Math.Cos(num)), (float)(Math.Cos(num2) * Math.Cos(num)), (float)Math.Sin(num));
	}

	public static Vector3 Right(Vector3 rot)
	{
		double num = (double)rot.Z * Math.PI / 180.0;
		return new Vector3((float)Math.Cos(num), (float)Math.Sin(num), 0f);
	}

	public static Vector3 LookRotation(Vector3 from, Vector3 to)
	{
		Vector3 vector = to - from;
		float num = (float)Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
		float x = (float)(Math.Atan2(vector.Z, num) * 180.0 / Math.PI);
		float z = (float)(Math.Atan2(0f - vector.X, vector.Y) * 180.0 / Math.PI);
		return new Vector3(x, 0f, z);
	}

	public static float LerpAngle(float a, float b, float t)
	{
		float num = ((b - a) % 360f + 540f) % 360f - 180f;
		return a + num * t;
	}

	public static float Smooth(float t)
	{
		if (t <= 0f)
		{
			return 0f;
		}
		if (t >= 1f)
		{
			return 1f;
		}
		return t * t * (3f - 2f * t);
	}
}
