using System;
using System.Collections.Generic;
using System.Threading;

namespace KickChaos
{
    /// <summary>Entrada de red acotada. El lector espera espacio para eventos
    /// importantes; el exceso de chat se descarta sin expulsar suscripciones.</summary>
    public sealed class KickEventInbox
    {
        readonly object gate = new object();
        readonly LinkedList<KickEvent> events = new LinkedList<KickEvent>();
        readonly int capacity;
        public KickEventInbox(int capacity) { this.capacity = Math.Max(16, Math.Min(8192, capacity)); }
        public int Count { get { lock (gate) return events.Count; } }
        public bool IsEmpty { get { return Count == 0; } }
        public bool TryEnqueue(KickEvent ev)
        {
            lock (gate)
            {
                if (events.Count >= capacity)
                {
                    if (ev.Kind == KickEventKind.Chat) return true;
                    var node = events.First;
                    while (node != null && node.Value.Kind != KickEventKind.Chat) node = node.Next;
                    if (node == null) return false;
                    events.Remove(node);
                }
                events.AddLast(ev);
                return true;
            }
        }
        public bool TryDequeue(out KickEvent ev)
        {
            lock (gate)
            {
                if (events.Count == 0) { ev = null; return false; }
                ev = events.First.Value;
                events.RemoveFirst();
                return true;
            }
        }
    }

    /// <summary>Demora cada evento una sola vez. Si la cola de acciones se llena,
    /// conserva el evento y espera espacio en vez de volver a dispararlo.</summary>
    public sealed class DelayedKickEvents
    {
        sealed class Entry
        {
            public KickEvent Event;
            public double Due;
            public long Sequence;
        }
        readonly List<Entry> entries = new List<Entry>();
        long sequence;
        public int Count { get { return entries.Count; } }
        public int DroppedChats { get; private set; }
        public void Clear() { entries.Clear(); }

        public bool TryEnqueue(KickEvent ev, double now, Config cfg)
        {
            if (ev == null) return true;
            int capacity = Math.Max(1, Math.Min(2048, cfg.KickDelayCapacity));
            if (entries.Count >= capacity)
            {
                // Primero se protegen subs, regalos y donaciones. Un chat lleno
                // se descarta; un evento importante vuelve a intentarse al avanzar.
                if (ev.Kind == KickEventKind.Chat) { DroppedChats++; return true; }
                int chat = entries.FindIndex(e => e.Event.Kind == KickEventKind.Chat);
                if (chat < 0) return false;
                entries.RemoveAt(chat);
                DroppedChats++;
            }
            double due = now + cfg.EventDelay(ev);
            // La recepción ya fija el momento: los reintentos no reinician el delay.
            if (ev.ReceivedUtc != DateTime.MinValue && !ev.Simulated)
            {
                double age = Math.Max(0, (DateTime.UtcNow - ev.ReceivedUtc).TotalSeconds);
                due = now + Math.Max(0, cfg.EventDelay(ev) - age);
            }
            var entry = new Entry { Event = ev, Due = due, Sequence = sequence++ };
            int index = entries.BinarySearch(entry, EntryComparer.Instance);
            entries.Insert(index < 0 ? ~index : index, entry);
            return true;
        }

        public KickEvent TryDequeue(double now)
        {
            if (entries.Count == 0 || now < entries[0].Due) return null;
            KickEvent ev = entries[0].Event;
            entries.RemoveAt(0);
            return ev;
        }

        sealed class EntryComparer : IComparer<Entry>
        {
            public static readonly EntryComparer Instance = new EntryComparer();
            public int Compare(Entry a, Entry b)
            {
                int due = a.Due.CompareTo(b.Due);
                return due != 0 ? due : a.Sequence.CompareTo(b.Sequence);
            }
        }
    }

    public static class KickDonation
    {
        public static bool IsDonation(KickEvent ev)
        {
            return ev != null && (string.Equals(ev.EventName, "KicksGifted", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ev.EventName, "KicksGiftedEvent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ev.EventName, "KicksDonationEvent", StringComparison.OrdinalIgnoreCase));
        }

        public static double CameraSeconds(int amount, Config cfg)
        {
            if (amount <= 0 || !cfg.Ini.GetBool("CamaraKicks", "Activa", true)) return 0;
            int unit = Math.Max(1, cfg.Ini.GetInt("CamaraKicks", "Cantidad", 100));
            float seconds = Config.SafeSeconds(cfg.Ini.GetFloat("CamaraKicks", "Segundos", 10), 10, 300);
            float min = Config.SafeSeconds(cfg.Ini.GetFloat("CamaraKicks", "MinimoSegundos", 5), 5, 300);
            float max = Config.SafeSeconds(cfg.Ini.GetFloat("CamaraKicks", "MaximoSegundos", 45), 45, 300);
            if (max < min) max = min;
            if (seconds <= 0 || max <= 0) return 0;
            return Math.Max(min, Math.Min(max, amount / (double)unit * seconds));
        }

        public static KickEvent TestSubscription(string source, int count)
        {
            string key = Config.Normalize(source ?? "Suscripcion");
            return new KickEvent { Kind = key.Contains("regalo") ? KickEventKind.GiftedSubs :
                key.Contains("follow") ? KickEventKind.Follow : KickEventKind.Subscription,
                EventName = source ?? "Suscripcion", User = "prueba", Count = Math.Max(1, count), Simulated = true };
        }

        public static bool StartsDirector(QueuedAction action, bool followOnSpawn, bool active)
        {
            return action != null && action.Name != null && action.Name.StartsWith("Npc", StringComparison.Ordinal) &&
                followOnSpawn && !active;
        }
    }
}
