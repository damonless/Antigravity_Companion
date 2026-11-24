using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AntigravityCompanion
{
    /// <summary>
    /// Bounded-queue SSE broadcast hub driven by a single background pump thread (P2-2).
    /// Eliminates permanent ThreadPool worker blocking per SSE client.
    /// Strictly C# 5.0 (.NET Framework 4.x).
    /// </summary>
    internal sealed class SseHub : IDisposable
    {
        public const int MaxClientQueueCapacity = 128;
        private const int KeepAliveIntervalSeconds = 10;
        private const int PumpWaitMs = 2000;

        private static readonly byte[] PingFrameBytes = Encoding.UTF8.GetBytes(": ping\n\n");

        internal sealed class ClientQueueState
        {
            public readonly TcpClient Tcp;
            public readonly NetworkStream Stream;
            public readonly object QueueLock = new object();
            public readonly Queue<byte[]> Queue = new Queue<byte[]>();
            public DateTime LastSentUtc;
            public volatile bool Closed;
            public long DroppedFrames;

            public ClientQueueState(TcpClient tcp, NetworkStream stream)
            {
                Tcp = tcp;
                Stream = stream;
                LastSentUtc = DateTime.UtcNow;
            }

            public void EnqueueBounded(byte[] frameBytes)
            {
                if (frameBytes == null || Closed) return;
                lock (QueueLock)
                {
                    if (Closed) return;
                    while (Queue.Count >= MaxClientQueueCapacity)
                    {
                        Queue.Dequeue();
                        DroppedFrames++;
                    }
                    Queue.Enqueue(frameBytes);
                }
            }

            public List<byte[]> DrainPending()
            {
                lock (QueueLock)
                {
                    if (Queue.Count == 0) return null;
                    List<byte[]> batch = new List<byte[]>(Queue.Count);
                    while (Queue.Count > 0)
                    {
                        batch.Add(Queue.Dequeue());
                    }
                    return batch;
                }
            }

            public void CloseQuietly()
            {
                Closed = true;
                lock (QueueLock)
                {
                    Queue.Clear();
                }
                try { Stream.Close(); } catch { }
                try { Tcp.Close(); } catch { }
            }
        }

        private readonly object _clientsLock = new object();
        private readonly List<ClientQueueState> _clients = new List<ClientQueueState>();
        private readonly AutoResetEvent _pumpSignal = new AutoResetEvent(false);
        private readonly Action _onClientActivity;

        private Thread _pumpThread;
        private volatile bool _running;
        private int _activeCount;

        public SseHub(Action onClientActivity)
        {
            _onClientActivity = onClientActivity;
        }

        public int ActiveCount
        {
            get { return Interlocked.CompareExchange(ref _activeCount, 0, 0); }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _pumpThread = new Thread(PumpLoop);
            _pumpThread.IsBackground = true;
            _pumpThread.Name = "AntigravityCompanion_SsePump";
            _pumpThread.Start();
        }

        public void RegisterClient(TcpClient tcp, NetworkStream stream)
        {
            if (tcp == null || stream == null) return;
            if (!_running)
            {
                try { stream.Close(); } catch { }
                try { tcp.Close(); } catch { }
                return;
            }

            try
            {
                tcp.SendTimeout = 5000;
            }
            catch { }

            ClientQueueState state = new ClientQueueState(tcp, stream);
            lock (_clientsLock)
            {
                if (!_running)
                {
                    state.CloseQuietly();
                    return;
                }
                _clients.Add(state);
                Interlocked.Exchange(ref _activeCount, _clients.Count);
            }

            try { _pumpSignal.Set(); } catch { }
        }

        public void Broadcast(string eventName, object payload)
        {
            if (!_running) return;

            string safeEvent = (eventName ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("\0", string.Empty);
            Dictionary<string, object> envelope = new Dictionary<string, object>();
            envelope["event"] = safeEvent;
            envelope["payload"] = payload;
            string json = UnifiedJson.Serialize(envelope, false);
            string frame = "event: " + safeEvent + "\ndata: " + json + "\n\n";
            byte[] frameBytes = Encoding.UTF8.GetBytes(frame);

            lock (_clientsLock)
            {
                for (int i = 0; i < _clients.Count; i++)
                {
                    ClientQueueState c = _clients[i];
                    if (c != null && !c.Closed)
                    {
                        c.EnqueueBounded(frameBytes);
                    }
                }
            }

            try { _pumpSignal.Set(); } catch { }
        }

        public void Stop()
        {
            _running = false;
            try { _pumpSignal.Set(); } catch { }

            List<ClientQueueState> toClose;
            lock (_clientsLock)
            {
                toClose = new List<ClientQueueState>(_clients);
                _clients.Clear();
                Interlocked.Exchange(ref _activeCount, 0);
            }

            for (int i = 0; i < toClose.Count; i++)
            {
                toClose[i].CloseQuietly();
            }

            Thread t = _pumpThread;
            _pumpThread = null;
            if (t != null && t.IsAlive && Thread.CurrentThread != t)
            {
                try { t.Join(1000); } catch { }
            }
        }

        public void Dispose()
        {
            Stop();
            try { _pumpSignal.Dispose(); } catch { }
        }

        private void PumpLoop()
        {
            while (_running)
            {
                try
                {
                    _pumpSignal.WaitOne(PumpWaitMs);
                }
                catch
                {
                    if (!_running) break;
                }

                if (!_running) break;

                List<ClientQueueState> snapshot;
                lock (_clientsLock)
                {
                    if (_clients.Count == 0) continue;
                    snapshot = new List<ClientQueueState>(_clients);
                }

                DateTime nowUtc = DateTime.UtcNow;
                List<ClientQueueState> deadClients = null;

                for (int i = 0; i < snapshot.Count; i++)
                {
                    ClientQueueState c = snapshot[i];
                    if (c == null || c.Closed)
                    {
                        if (deadClients == null) deadClients = new List<ClientQueueState>();
                        deadClients.Add(c);
                        continue;
                    }

                    List<byte[]> batch = c.DrainPending();
                    bool needPing = (batch == null || batch.Count == 0) &&
                                    (nowUtc - c.LastSentUtc).TotalSeconds >= KeepAliveIntervalSeconds;

                    if ((batch == null || batch.Count == 0) && !needPing)
                    {
                        continue;
                    }

                    try
                    {
                        if (batch != null && batch.Count > 0)
                        {
                            for (int b = 0; b < batch.Count; b++)
                            {
                                byte[] fb = batch[b];
                                if (fb != null && fb.Length > 0)
                                {
                                    c.Stream.Write(fb, 0, fb.Length);
                                }
                            }
                        }
                        else if (needPing)
                        {
                            c.Stream.Write(PingFrameBytes, 0, PingFrameBytes.Length);
                        }

                        c.Stream.Flush();
                        c.LastSentUtc = DateTime.UtcNow;
                        if (_onClientActivity != null) _onClientActivity();
                    }
                    catch
                    {
                        c.CloseQuietly();
                        if (deadClients == null) deadClients = new List<ClientQueueState>();
                        deadClients.Add(c);
                    }
                }

                if (deadClients != null && deadClients.Count > 0)
                {
                    lock (_clientsLock)
                    {
                        for (int i = 0; i < deadClients.Count; i++)
                        {
                            _clients.Remove(deadClients[i]);
                        }
                        Interlocked.Exchange(ref _activeCount, _clients.Count);
                    }
                }
            }
        }
    }
}
