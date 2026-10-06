using System;
using System.Collections.Generic;

namespace KickChaos
{
    // Shared contract for the game HUD and the standalone administration panel.
    // Coordinates are GTA IV world coordinates; Health is a percentage (0..100).
    public sealed class StreamNpcRow
    {
        public int Id, Ped, ParentId, Kills, Characters, Level, Vehicle;
        public string Name = "", Owner = "", Kind = "SUB", State = "", Objective = "", Build = "SUPERVIVIENTE", Perks = "", VehicleName = "";
        public float Health = 100, Armor, X, Y, Z, Heading;
        public double CameraReadyIn, RecentUntil;
        public bool InCar, Driving, Combat, Pursuit, Racing, Claimable, CameraAvailable = true;
    }

    public sealed class StreamFeedEntry
    {
        public string Kind = "", User = "", Text = "";
        public double At, Expires;
        public int Amount;
    }

    public sealed class StreamMarker
    {
        public string Kind = "", Name = "";
        public float X, Y, Z;
    }

    public sealed class StreamSnapshot
    {
        public string Version = "1.9.1-stream", UpdatedUtc = "", KickStatus = "", CameraName = "", Debug = "", SessionId = "";
        public bool Active, Following;
        public int FollowedPed;
        public float CameraX, CameraY, CameraZ, CameraHeading, CameraFov = 45;
        public double CityRemainingSeconds;
        public List<StreamNpcRow> Npcs = new List<StreamNpcRow>();
        public List<StreamFeedEntry> Feed = new List<StreamFeedEntry>();
        public List<StreamMarker> Markers = new List<StreamMarker>();
    }

    public sealed class StreamAdminCommand
    {
        public string Id = "", Action = "", Value = "", Zone = "", SessionId = "";
        public int NpcId;
        public float X, Y, Z;
        public double Seconds = 15;
    }

    public static class StreamRuntime
    {
        static readonly object gate = new object();
        static readonly List<StreamFeedEntry> feed = new List<StreamFeedEntry>();
        public static StreamSnapshot Snapshot = new StreamSnapshot();
        public static void AddFeed(string kind, string user, string text, double now, int amount = 0)
        {
            lock (gate)
            {
                feed.Add(new StreamFeedEntry { Kind = kind, User = user, Text = text, At = now, Expires = now + 20, Amount = amount });
                while (feed.Count > 40) feed.RemoveAt(0);
            }
        }
        public static List<StreamFeedEntry> Feed(double now)
        {
            lock (gate)
            {
                feed.RemoveAll(e => e.Expires <= now);
                return new List<StreamFeedEntry>(feed);
            }
        }
    }
}
