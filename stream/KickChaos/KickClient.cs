using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace KickChaos;

public class KickClient
{
	private readonly Dictionary<string, DateTime> polls = new Dictionary<string, DateTime>();

	public readonly KickEventInbox Events;

	public volatile bool Connected;

	public volatile string Status = "sin iniciar";

	private long messagesReceived;

	private readonly Config cfg;

	private readonly Action<string> log;

	private Thread thread;

	private volatile bool stopping;

	private ClientWebSocket ws;

	private HttpWebRequest lookupRequest;

	private CancellationTokenSource runCancellation;

	private readonly object lifecycleLock = new object();

	private readonly object frameLock = new object();

	private readonly object sendLock = new object();

	private long lastReceiveTicks = DateTime.UtcNow.Ticks;

	private long lastPingTicks = DateTime.UtcNow.Ticks;

	private const int MaxFrameBytes = 1024 * 1024;

	private readonly Queue<string> seenOrder = new Queue<string>();

	private readonly HashSet<string> seen = new HashSet<string>();

	private readonly Dictionary<string, DateTime> recentEvents = new Dictionary<string, DateTime>();

	private readonly Queue<KeyValuePair<string, DateTime>> recentOrder = new Queue<KeyValuePair<string, DateTime>>();

	private readonly HashSet<string> loggedUnknown = new HashSet<string>();

	public readonly ConcurrentDictionary<string, DateTime> SeenOther = new ConcurrentDictionary<string, DateTime>();

	public long MessagesReceived => Interlocked.Read(ref messagesReceived);

	public KickClient(Config cfg, Action<string> log)
	{
		this.cfg = cfg;
		Events = new KickEventInbox(cfg.Ini.GetInt("DelayKick", "ColaRecepcionMaxima", 2048));
		this.log = log ?? ((Action<string>)delegate
		{
		});
	}

	public void Start()
	{
		lock (lifecycleLock)
		{
			if (thread != null && thread.IsAlive) return;
			stopping = false;
			runCancellation = new CancellationTokenSource();
			CancellationToken token = runCancellation.Token;
			thread = new Thread(() => Run(token))
			{
				IsBackground = true,
				Name = "KickChaos-Kick"
			};
			thread.Start();
		}
	}

	public void Stop()
	{
		Thread worker;
		ClientWebSocket socket;
		HttpWebRequest request;
		lock (lifecycleLock)
		{
			stopping = true;
			runCancellation?.Cancel();
			worker = thread;
			socket = ws;
			request = lookupRequest;
		}
		try
		{
			socket?.Abort();
			request?.Abort();
		}
		catch
		{
		}
		try
		{
			if (worker != null && worker != Thread.CurrentThread) worker.Join(250); // corto: no congelar el juego al recargar (el hilo termina solo)
		}
		catch
		{
		}
		// A worker still finishing cancellation keeps ownership until Run's finally.
		// Thread.Abort could terminate it while holding the log/socket locks.
		lock (lifecycleLock)
		{
			if (thread == null || thread == worker)
			{
				Connected = false;
				Status = "detenido";
			}
		}
	}

	private string BuildUrl()
	{
		if (!string.IsNullOrEmpty(cfg.WebsocketUrlOverride))
		{
			return cfg.WebsocketUrlOverride;
		}
		return "wss://ws-" + cfg.PusherCluster + ".pusher.com/app/" + cfg.PusherKey + "?protocol=7&client=js&version=8.4.0&flash=false";
	}

	private void Run(CancellationToken token)
	{
		try
		{
		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
		}
		catch
		{
		}
		int num = 0;
		while (!token.IsCancellationRequested)
		{
			try
			{
				if (cfg.ChatroomId <= 0)
				{
					Status = "buscando id del chat de " + cfg.Channel + "...";
					if (!TryLookupIds(cfg.Channel, out long chatroomId, out long channelId))
						throw new InvalidOperationException("No pude obtener el ChatroomId; configura ChatroomId y ChannelId en config.ini si Kick bloquea la consulta");
					cfg.ChatroomId = chatroomId;
					cfg.ChannelId = channelId;
				}
				token.ThrowIfCancellationRequested();
				ConnectAndListen(token);
				num = 0;
			}
			catch (Exception ex)
			{
				if (token.IsCancellationRequested)
				{
					break;
				}
				Status = "desconectado: " + Short(ex);
				log("[Kick] " + Status);
			}
			Connected = false;
			if (token.IsCancellationRequested)
			{
				break;
			}
			int num2 = Math.Min(30000, 1000 * (1 << Math.Min(num, 5)));
			num++;
			Status = "reconectando en " + num2 / 1000 + " s";
			if (token.WaitHandle.WaitOne(num2))
			{
				break;
			}
		}
		}
		finally
		{
			Connected = false;
			if (token.IsCancellationRequested) Status = "detenido";
			lock (lifecycleLock)
			{
				if (thread == Thread.CurrentThread)
				{
					thread = null;
					runCancellation?.Dispose();
					runCancellation = null;
				}
			}
		}
	}

	private static string Short(Exception ex)
	{
		Exception ex2 = ex;
		while (ex2 is AggregateException && ex2.InnerException != null)
		{
			ex2 = ex2.InnerException;
		}
		string text = ex2.Message;
		if (ex2.InnerException != null)
		{
			text = text + " (" + ex2.InnerException.Message + ")";
		}
		return text;
	}

	private void ConnectAndListen(CancellationToken token)
	{
		using (ClientWebSocket socket = new ClientWebSocket())
		using (CancellationTokenSource connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(token))
		using (MemoryStream memoryStream = new MemoryStream())
		{
		Thread watchdog = null;
		socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20.0);
		try
		{
			socket.Options.SetRequestHeader("Origin", "https://kick.com");
		}
		catch
		{
		}
		try
		{
			lock (lifecycleLock) ws = socket;
			Status = "conectando...";
			using (CancellationTokenSource connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(token))
			{
				connectCancellation.CancelAfter(TimeSpan.FromSeconds(20));
				socket.ConnectAsync(new Uri(BuildUrl()), connectCancellation.Token).GetAwaiter().GetResult();
			}
			Interlocked.Exchange(ref lastReceiveTicks, DateTime.UtcNow.Ticks);
			Interlocked.Exchange(ref lastPingTicks, DateTime.UtcNow.Ticks);
			watchdog = new Thread(() => Watchdog(socket, connectionCancellation.Token))
			{
				IsBackground = true,
				Name = "KickChaos-Ping"
			};
			watchdog.Start();
			byte[] array = new byte[16384];
			while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
			{
				ArraySegment<byte> buffer = new ArraySegment<byte>(array);
				WebSocketReceiveResult result = socket.ReceiveAsync(buffer, token).GetAwaiter().GetResult();
				if (result.MessageType == WebSocketMessageType.Close)
				{
					Status = "el servidor cerro la conexion (" + result.CloseStatusDescription + ")";
					break;
				}
				Interlocked.Exchange(ref lastReceiveTicks, DateTime.UtcNow.Ticks);
				if (result.MessageType != WebSocketMessageType.Text)
				{
					memoryStream.SetLength(0);
					continue;
				}
				if (memoryStream.Length + result.Count > MaxFrameBytes)
					throw new InvalidDataException("Mensaje de Kick demasiado grande");
				memoryStream.Write(array, 0, result.Count);
				if (result.EndOfMessage)
				{
					string text = Encoding.UTF8.GetString(memoryStream.ToArray());
					memoryStream.SetLength(0L);
					try
					{
						HandleFrame(text);
					}
					catch (Exception ex)
					{
						log("[Kick] error procesando mensaje: " + ex.Message);
					}
				}
			}
		}
		finally
		{
			Connected = false;
			connectionCancellation.Cancel();
			try
			{
				socket.Abort();
			}
			catch
			{
			}
			if (watchdog != null) watchdog.Join(2000);
			lock (lifecycleLock)
			{
				if (ws == socket) ws = null;
			}
		}
		}
	}

	private void Watchdog(ClientWebSocket socket, CancellationToken token)
	{
		while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
		{
			if (token.WaitHandle.WaitOne(1000)) break;
			DateTime utcNow = DateTime.UtcNow;
			if ((utcNow - new DateTime(Interlocked.Read(ref lastReceiveTicks), DateTimeKind.Utc)).TotalSeconds > 150.0)
			{
				log("[Kick] sin datos hace 150 s, reconectando");
				try
				{
					socket.Abort();
					break;
				}
				catch
				{
					break;
				}
			}
			if ((utcNow - new DateTime(Interlocked.Read(ref lastPingTicks), DateTimeKind.Utc)).TotalSeconds > 60.0)
			{
				Interlocked.Exchange(ref lastPingTicks, utcNow.Ticks);
				Send(socket, "{\"event\":\"pusher:ping\",\"data\":{}}", token);
			}
		}
	}

	private void Send(ClientWebSocket socket, string text, CancellationToken cancellation = default(CancellationToken))
	{
		try
		{
			lock (sendLock)
			{
				if (stopping || socket.State != WebSocketState.Open) return;
				byte[] bytes = Encoding.UTF8.GetBytes(text);
				using (CancellationTokenSource sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
				{
					sendCancellation.CancelAfter(TimeSpan.FromSeconds(10));
					socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, sendCancellation.Token).GetAwaiter().GetResult();
				}
			}
		}
		catch (Exception ex)
		{
			if (!stopping) log("[Kick] error enviando: " + Short(ex));
			try { socket.Abort(); } catch { }
		}
	}

	private void Subscribe(string channel)
	{
		ClientWebSocket clientWebSocket = ws;
		if (clientWebSocket != null)
		{
			Send(clientWebSocket, "{\"event\":\"pusher:subscribe\",\"data\":{\"auth\":\"\",\"channel\":" + Json.Quote(channel) + "}}");
		}
	}

	public void HandleFrame(string text)
	{
		if (string.IsNullOrWhiteSpace(text) || text.Length > MaxFrameBytes) return;
		lock (frameLock) HandleFrameLocked(text);
	}

	private void HandleFrameLocked(string text)
	{
		Dictionary<string, object> dictionary = Json.ParseObject(text);
		if (dictionary == null)
		{
			return;
		}
		string text2 = Json.GetString(dictionary, "event") ?? string.Empty;
		string text3 = Json.GetString(dictionary, "channel") ?? string.Empty;
		if (text2.Length == 0) return;
		switch (text2)
		{
		case "pusher:connection_established":
			Connected = false;
			Status = "conectado a Kick, suscribiendo al chat de " + cfg.Channel;
			log("[Kick] " + Status + " (chatroom " + cfg.ChatroomId + ")");
			Subscribe("chatrooms." + cfg.ChatroomId + ".v2");
			Subscribe("chatroom_" + cfg.ChatroomId);
			if (cfg.ChannelId > 0)
			{
				Subscribe("channel." + cfg.ChannelId);
				Subscribe("channel_" + cfg.ChannelId);
			}
			return;
		case "pusher:ping":
		{
			ClientWebSocket clientWebSocket = ws;
			if (clientWebSocket != null)
			{
				Send(clientWebSocket, "{\"event\":\"pusher:pong\",\"data\":{}}");
			}
			return;
		}
		case "pusher:pong":
			return;
		case "pusher_internal:subscription_succeeded":
			log("[Kick] suscripto a " + text3);
			if (text3 == "chatrooms." + cfg.ChatroomId + ".v2" || text3 == "chatroom_" + cfg.ChatroomId)
			{
				Connected = true;
				Status = "conectado al chat de " + cfg.Channel;
			}
			return;
		case "pusher:error":
			Status = "error de suscripcion de Kick; revisa los IDs del canal y log.txt";
			log("[Kick] error de Pusher: " + text);
			return;
		}
		if (!text2.StartsWith("pusher", StringComparison.Ordinal))
		{
			object obj = Json.Get(dictionary, "data");
			string text4 = obj as string;
			object data;
			try
			{
				data = text4 == null ? obj : Json.Parse(text4);
				if (!(data is Dictionary<string, object>)) return;
				text4 = Json.Canonical(data);
			}
			catch (FormatException)
			{
				log("[Kick] mensaje JSON invalido ignorado");
				return;
			}
			string text5 = text2;
			int num = text5.LastIndexOf('\\');
			if (num >= 0)
			{
				text5 = text5.Substring(num + 1);
			}
			KickEvent kickEvent = Translate(text5, data, text4);
			if (kickEvent != null)
			{
				// La entrada conserva este evento mientras el juego está pausado o
				// cargando. No crece sin límite ni se pierde una sub por falta de lugar.
				while (!Events.TryEnqueue(kickEvent))
				{
					CancellationTokenSource cancellation = runCancellation;
					if (stopping || (cancellation != null && cancellation.IsCancellationRequested)) return;
					Thread.Sleep(25);
				}
				Interlocked.Increment(ref messagesReceived);
			}
		}
	}

	private KickEvent Translate(string name, object data, string raw)
	{
		KickEvent kickEvent = new KickEvent();
		kickEvent.EventName = name;
		kickEvent.Raw = ((raw.Length <= 400) ? raw : raw.Substring(0, 400));
		KickEvent kickEvent2 = kickEvent;
		switch (name)
		{
		case "ChatMessageEvent":
		{
			string text2 = Json.GetString(data, "id") ?? string.Empty;
			if (text2.Length > 0 && !Remember("m:" + text2))
			{
				return null;
			}
			kickEvent2.Kind = KickEventKind.Chat;
			kickEvent2.User = Json.GetString(data, "sender.username") ?? string.Empty;
			kickEvent2.Text = Json.GetString(data, "content") ?? string.Empty;
			kickEvent2.Level = LevelFromBadges(Json.GetList(data, "sender.identity.badges"));
			kickEvent2.Color = Json.GetString(data, "sender.identity.color") ?? string.Empty;
			string a = Json.GetString(data, "sender.slug") ?? string.Empty;
			if (string.Equals(a, cfg.Channel, StringComparison.OrdinalIgnoreCase))
			{
				kickEvent2.Level = 4;
			}
			return kickEvent2;
		}
		case "SubscriptionEvent":
		case "ChannelSubscriptionEvent":
			kickEvent2.Kind = KickEventKind.Subscription;
			kickEvent2.User = FirstString(data, "username", "user.username", "subscriber.username", "subscription.user.username");
			// Some channel events carry counters/user IDs only: they are not new named subscribers.
			if (kickEvent2.User.Length == 0) return null;
			kickEvent2.Count = Math.Max(1, Json.GetInt(data, "months", Json.GetInt(data, "subscription.months", 1)));
			if (!RememberDelivery("Subscription", SubscriptionKey(kickEvent2, data), data))
			{
				return null;
			}
			return kickEvent2;
		case "GiftedSubscriptionsEvent":
			kickEvent2.Kind = KickEventKind.GiftedSubs;
			kickEvent2.User = FirstString(data, "gifter_username", "gifter.username", "sender.username", "username");
			List<object> gifted = Json.GetList(data, "gifted_usernames") ?? Json.GetList(data, "gifted_users");
			if (gifted != null)
			{
				HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (object recipient in gifted)
				{
					string username = (recipient as string) ?? FirstString(recipient, "username", "user.username");
					username = username.Trim();
					if (username.Length > 0 && unique.Add(username)) kickEvent2.Recipients.Add(username);
				}
			}
			kickEvent2.Count = Math.Max(1, gifted?.Count ?? Json.GetInt(data, "count", 1));
			if (!RememberDelivery("GiftedSubscriptions", GiftKey(kickEvent2, data, raw), data)) return null;
			return kickEvent2;
		case "KicksGifted":
		case "KicksGiftedEvent":
		case "KicksDonationEvent":
			kickEvent2.Kind = KickEventKind.Other;
			kickEvent2.EventName = "KicksGifted";
			kickEvent2.User = FirstString(data, "sender.username", "username", "gifter_username", "gifter.username", "user.username", "user_display_name", "sender_username");
			kickEvent2.Count = Json.GetInt(data, "gift.amount", Json.GetInt(data, "amount", Json.GetInt(data, "kicks", 0)));
			if (kickEvent2.User.Length == 0 || kickEvent2.Count <= 0) return null;
			string donationKey = Json.Quote(kickEvent2.User.ToLowerInvariant()) + "|" + kickEvent2.Count + "|" +
				FirstString(data, "event_id", "id", "gift.id", "created_at", "timestamp");
			if (!RememberDelivery("KicksGifted", donationKey, data)) return null;
			return kickEvent2;
		case "FollowersUpdated":
		{
			object obj = Json.Get(data, "followed");
			if (obj is bool && !(bool)obj)
			{
				return null;
			}
			kickEvent2.User = FirstString(data, "username", "user.username");
			// A follower-total refresh has no named user and must not create a phantom NPC.
			if (kickEvent2.User.Length == 0) return null;
			if (!RememberRecent(name, raw))
			{
				return null;
			}
			kickEvent2.Kind = KickEventKind.Follow;
			return kickEvent2;
		}
		case "StreamHostEvent":
			if (!RememberRecent(name, raw))
			{
				return null;
			}
			kickEvent2.Kind = KickEventKind.Host;
			kickEvent2.User = Json.GetString(data, "host_username") ?? string.Empty;
			kickEvent2.Count = Math.Max(1, Json.GetInt(data, "number_viewers", 1));
			return kickEvent2;
		case "UserBannedEvent":
			if (!RememberRecent(name, raw))
			{
				return null;
			}
			kickEvent2.Kind = KickEventKind.Ban;
			kickEvent2.User = Json.GetString(data, "user.username") ?? string.Empty;
			return kickEvent2;
		case "PinnedMessageCreatedEvent":
			if (!RememberRecent(name, raw))
			{
				return null;
			}
			kickEvent2.Kind = KickEventKind.Other;
			kickEvent2.EventName = "MensajeFijado";
			kickEvent2.User = Json.GetString(data, "message.sender.username") ?? string.Empty;
			kickEvent2.Text = Json.GetString(data, "message.content") ?? string.Empty;
			return kickEvent2;
		case "PollUpdateEvent":
		{
			string text = Json.GetString(data, "poll.title") ?? string.Empty;
			DateTime utcNow = DateTime.UtcNow;
			if (polls.TryGetValue(text, out var value) && (utcNow - value).TotalMinutes < 15.0)
			{
				polls[text] = utcNow;
				return null;
			}
			polls[text] = utcNow;
			if (polls.Count > 200) polls.Clear(); // que no crezca para siempre
			kickEvent2.Kind = KickEventKind.Other;
			kickEvent2.EventName = "Encuesta";
			kickEvent2.User = "encuesta";
			kickEvent2.Text = text;
			return kickEvent2;
		}
		case "ChatroomUpdatedEvent":
		case "ChatroomClearEvent":
		case "MessageDeletedEvent":
		case "PinnedMessageDeletedEvent":
		case "PollDeleteEvent":
		case "UserUnbannedEvent":
		case "LuckyUsersWhoGotGiftSubscriptionsEvent":
		case "GiftsLeaderboardUpdated":
		case "StreamerIsLive":
		case "StopStreamBroadcast":
		case "LivestreamUpdated":
		case "ChannelUpdated":
			return null;
		default:
			if (!RememberRecent(name, raw))
			{
				return null;
			}
			SeenOther[name] = DateTime.UtcNow;
			if (loggedUnknown.Add(name))
			{
				log("[Kick] evento no reconocido '" + name + "' (se puede mapear en [Eventos] con ese nombre). Datos: " + kickEvent2.Raw);
			}
			kickEvent2.Kind = KickEventKind.Other;
			kickEvent2.User = Json.GetString(data, "username") ?? Json.GetString(data, "sender.username") ?? Json.GetString(data, "gifter_username") ?? Json.GetString(data, "user.username") ?? Json.GetString(data, "user_display_name") ?? Json.GetString(data, "redeemer.username") ?? string.Empty;
			kickEvent2.Count = Math.Max(1, Json.GetInt(data, "gift.amount", Json.GetInt(data, "amount", 1)));
			return kickEvent2;
		}
	}

	private bool RememberDelivery(string name, string fallback, object data)
	{
		string identity = FirstString(data, "event_id", "id", "transaction_id");
		if (identity.Length > 0)
			return Remember("e:" + name + ":" + identity);
		return RememberRecent(name, fallback);
	}

	private static string FirstString(object data, params string[] paths)
	{
		foreach (string path in paths)
		{
			string value = Json.GetString(data, path);
			if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
		}
		return string.Empty;
	}

	private static string SubscriptionKey(KickEvent e, object data)
	{
		// Alias channels can wrap the same subscription differently. The subscriber,
		// tenure and event identity remain stable while channel names/key order do not.
		return Json.Quote(e.User.ToLowerInvariant()) + "|" + e.Count + "|" +
			FirstString(data, "event_id", "subscription_id", "subscription.id", "created_at", "timestamp");
	}

	private static string GiftKey(KickEvent e, object data, string raw)
	{
		if (e.Recipients.Count == 0) return raw;
		List<string> names = e.Recipients.ConvertAll(name => name.ToLowerInvariant());
		names.Sort(StringComparer.Ordinal);
		return Json.Quote(e.User.ToLowerInvariant()) + "|" + e.Count + "|" +
			string.Join(",", names.ToArray()) + "|" + FirstString(data, "event_id", "created_at", "timestamp");
	}

	private static int LevelFromBadges(List<object> badges)
	{
		int num = 0;
		if (badges == null)
		{
			return 0;
		}
		foreach (object badge in badges)
		{
			string text = (Json.GetString(badge, "type") ?? string.Empty).ToLowerInvariant();
			int num2 = 0;
			switch (text)
			{
			case "broadcaster":
				num2 = 4;
				break;
			case "moderator":
				num2 = 3;
				break;
			case "vip":
				num2 = 2;
				break;
			case "subscriber":
			case "founder":
			case "og":
				num2 = 1;
				break;
			}
			if (num2 > num)
			{
				num = num2;
			}
		}
		return num;
	}

	private bool Remember(string key)
	{
		if (seen.Contains(key))
		{
			return false;
		}
		seen.Add(key);
		seenOrder.Enqueue(key);
		while (seenOrder.Count > 1000)
		{
			seen.Remove(seenOrder.Dequeue());
		}
		return true;
	}

	private bool RememberRecent(string name, string raw)
	{
		string key = name + "|" + raw;
		DateTime utcNow = DateTime.UtcNow;
		if (recentEvents.TryGetValue(key, out var value) && (utcNow - value).TotalSeconds < 20.0)
		{
			return false;
		}
		recentEvents[key] = utcNow;
		recentOrder.Enqueue(new KeyValuePair<string, DateTime>(key, utcNow));
		while (recentOrder.Count > 2048 || (recentOrder.Count > 0 && (utcNow - recentOrder.Peek().Value).TotalSeconds > 60))
		{
			KeyValuePair<string, DateTime> expired = recentOrder.Dequeue();
			if (recentEvents.TryGetValue(expired.Key, out DateTime recorded) && recorded == expired.Value)
				recentEvents.Remove(expired.Key);
		}
		return true;
	}

	public bool TryLookupIds(string slug, out long chatroomId, out long channelId)
	{
		chatroomId = 0L;
		channelId = 0L;
		HttpWebRequest httpWebRequest = null;
		try
		{
			if (string.IsNullOrWhiteSpace(slug)) return false;
			httpWebRequest = (HttpWebRequest)WebRequest.Create("https://kick.com/api/v2/channels/" + Uri.EscapeDataString(slug.Trim()));
			lock (lifecycleLock)
			{
				if (stopping) return false;
				lookupRequest = httpWebRequest;
			}
			httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0 Safari/537.36";
			httpWebRequest.Accept = "application/json";
			httpWebRequest.Timeout = 15000;
			httpWebRequest.ReadWriteTimeout = 15000;
			using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
			{
				using StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.UTF8);
				object node = Json.Parse(streamReader.ReadToEnd());
				chatroomId = Json.GetLong(node, "chatroom.id");
				channelId = Json.GetLong(node, "id");
			}
			log("[Kick] ids obtenidos: chatroom " + chatroomId + ", canal " + channelId);
			return chatroomId > 0;
		}
		catch (Exception ex)
		{
			if (!stopping) log("[Kick] no se pudo consultar la API de Kick: " + ex.Message);
			return false;
		}
		finally
		{
			lock (lifecycleLock)
			{
				if (lookupRequest == httpWebRequest) lookupRequest = null;
			}
		}
	}
}
