using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    // One host and any number of guests, joined as a star: a guest talks to the host, and
    // the host passes messages on. Messages wait in a queue until the test lets them arrive,
    // so a test decides what happens before what.
    public sealed class SimNetwork
    {
        private readonly List<SimMessage> _queue = new List<SimMessage>();

        public readonly SimRules Rules;
        public readonly List<SimPeer> Peers = new List<SimPeer>();
        public int Sent;
        public int SentBundlePackets;
        private int _peersEver = 1;
        private Random _loss;
        private double _lossRate;
        private Random _duplicate;
        private double _duplicateRate;

        public SimNetwork(SimRules rules = null)
        {
            Rules = rules ?? new SimRules();
            Peers.Add(new SimPeer(this, "host", true, 0));
        }

        public SimPeer Host { get { return Peers[0]; } }

        public int InFlight { get { return _queue.Count; } }

        public SimPeer this[string name]
        {
            get { return Peers.First(p => p.Name == name); }
        }

        // A guest joins. Coop sends the host's whole world once, with every field.
        public SimPeer Join(string name)
        {
            var peer = new SimPeer(this, name, false, _peersEver++);
            Peers.Add(peer);
            _queue.Add(new SimMessage
            {
                From = Host.Name,
                Hop = Host.Name,
                To = name,
                Type = "Snapshot",
                Items = Host.Items.Values.Select(i => i.Clone()).ToList(),
            });
            return peer;
        }

        // A guest leaves. What was on its way to it, and what it had sent, is lost. Coop drops
        // what the guest held where it was, and tells the others.
        public void Disconnect(string name)
        {
            Peers.RemoveAll(p => p.Name == name && !p.IsHost);
            _queue.RemoveAll(m => m.To == name || m.From == name);
            if (Rules.CoopDropsLeaversItems)
                Host.DropItemsHeldBy(name);
        }

        // From now on each message is lost with this chance. The join snapshot is not.
        public void LoseMessages(double rate, int seed)
        {
            _lossRate = rate;
            _loss = new Random(seed);
        }

        // From now on each message is delivered twice with this chance. Coop's star network and
        // reliable sends should not do this; the fault shows what it would cost if they did.
        public void DuplicateMessages(double rate, int seed)
        {
            _duplicateRate = rate;
            _duplicate = new Random(seed);
        }

        private bool Lost(SimMessage m)
        {
            return _loss != null && m.Type != "Snapshot" && _loss.NextDouble() < _lossRate;
        }

        // From a peer: a host sends to every guest, a guest to the host.
        internal void Send(SimPeer from, string type, SimMessage message)
        {
            message.From = from.Name;
            message.Hop = from.Name;
            message.Type = type;
            Sent++;
            if (type == "Bundle")
                SentBundlePackets++;

            foreach (SimPeer to in Peers)
            {
                if (to == from)
                    continue;
                if (!from.IsHost && !to.IsHost)
                    continue;
                if (Lost(message))
                    continue;
                _queue.Add(Copy(message, to.Name));
                if (_duplicate != null && _duplicate.NextDouble() < _duplicateRate)
                    _queue.Add(Copy(message, to.Name));
            }
        }

        // From the host to one guest.
        internal void SendTo(SimPeer from, string to, string type, SimMessage message)
        {
            message.From = from.Name;
            message.Hop = from.Name;
            message.Type = type;
            Sent++;
            if (!Lost(message))
                _queue.Add(Copy(message, to));
        }

        private static SimMessage Copy(SimMessage m, string to)
        {
            return new SimMessage
            {
                From = m.From,
                Holder = m.Holder,
                Hop = m.Hop,
                To = to,
                Type = m.Type,
                Body = m.Body,
                Items = m.Items == null ? null : m.Items.Select(i => i.Clone()).ToList(),
                Id = m.Id,
                X = m.X,
            };
        }

        // Every peer's end of frame, host first.
        public void EndFrame()
        {
            foreach (SimPeer peer in Peers)
                peer.EndFrame();
        }

        // Delivers the message that has waited longest. False when none is waiting.
        public bool Step()
        {
            return _queue.Count != 0 && Deliver(0);
        }

        // Delivers the message at this place in the queue.
        private bool Deliver(int index)
        {
            SimMessage message = _queue[index];
            _queue.RemoveAt(index);
            SimPeer to = Peers.FirstOrDefault(p => p.Name == message.To);
            if (to == null)
                return true;

            to.Receive(message);

            // The host settles a guest's bundle change and tells everyone, the sender too.
            if (message.Type == "Bundle" && to.IsHost && Rules.HostArbitrates && SyncRules.ShouldArbitrate(true))
            {
                to.Answer(message);
                return true;
            }

            // The host passes a guest's message on to the other guests.
            if (to.IsHost && Rules.CoopHostRelays && SyncRules.ShouldRelay(true) && message.Type != "PickRequest")
            {
                foreach (SimPeer other in Peers)
                {
                    if (other.IsHost || other.Name == message.From)
                        continue;
                    if (!Lost(message))
                    {
                        SimMessage relayed = Copy(message, other.Name);
                        relayed.Hop = to.Name;
                        _queue.Add(relayed);
                    }
                }
            }

            // Health and amount go in Coop's own packet; once it has passed that on, the host
            // settles the bundle (J-031).
            if (message.Type == "ItemUpdate" && to.IsHost && Rules.HostSettlesHealth && SyncRules.ShouldArbitrate(true))
                foreach (SimItem sent in message.Items)
                    to.Answer(sent.Id);
            return true;
        }

        // Delivers everything, with the links taking turns at random. Messages on one link
        // keep their order, as they do over a reliable connection, but one link may run well
        // ahead of another. The same seed gives the same turns.
        public void FlushInterleaved(int seed)
        {
            var random = new Random(seed);
            int guard = 1000000;
            while (_queue.Count > 0)
            {
                if (--guard == 0)
                    throw new InvalidOperationException("The messages never stopped.");
                var firsts = new List<int>();
                var seen = new HashSet<string>();
                for (int i = 0; i < _queue.Count; i++)
                {
                    if (seen.Add(_queue[i].Hop + ">" + _queue[i].To))
                        firsts.Add(i);
                }
                Deliver(firsts[random.Next(firsts.Count)]);
            }
        }

        // Like Settle, with the links interleaved.
        public void SettleInterleaved(int seed)
        {
            for (int round = 0; round < 50; round++)
            {
                EndFrame();
                if (_queue.Count == 0)
                    return;
                FlushInterleaved(seed + round);
            }
            throw new InvalidOperationException("The peers keep sending each other changes.");
        }

        // The pieces of a prefab across all items, as one peer counts them.
        public int Total(SimPeer peer, int prefab)
        {
            return peer.Items.Values.Where(i => i.Prefab == prefab).Sum(i => i.Count);
        }

        public void Flush()
        {
            int guard = 100000;
            while (Step())
            {
                if (--guard == 0)
                    throw new InvalidOperationException("The messages never stopped.");
            }
        }

        // Frames and deliveries until nothing is left to send. Throws when it never settles,
        // which is how a message loop shows up.
        public void Settle()
        {
            for (int round = 0; round < 50; round++)
            {
                EndFrame();
                if (_queue.Count == 0)
                    return;
                Flush();
            }
            throw new InvalidOperationException("The peers keep sending each other changes.");
        }

        // Changes the order messages will arrive in. The same seed gives the same order.
        public void Shuffle(int seed)
        {
            var random = new Random(seed);
            for (int i = _queue.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                SimMessage swap = _queue[i];
                _queue[i] = _queue[j];
                _queue[j] = swap;
            }
        }

        // Empty when every peer has the same items. Otherwise one line for each difference,
        // with the host as the first side.
        public List<string> Differences()
        {
            var found = new List<string>();
            foreach (SimPeer peer in Peers.Skip(1))
            {
                foreach (int id in Host.Items.Keys.Union(peer.Items.Keys).OrderBy(i => i))
                {
                    SimItem a, b;
                    bool hasA = Host.Items.TryGetValue(id, out a);
                    bool hasB = peer.Items.TryGetValue(id, out b);
                    if (!hasA || !hasB)
                    {
                        found.Add("#" + id + " is only on " + (hasA ? "host" : peer.Name));
                        continue;
                    }
                    if (a.Prefab != b.Prefab || a.Count != b.Count || a.Health != b.Health || a.Dried != b.Dried || a.Hung != b.Hung || a.Color != b.Color || a.Width != b.Width)
                        found.Add("#" + id + " differs: host " + a + " / " + peer.Name + " " + b);
                    else if (Math.Abs(a.X - b.X) > 0.0001)
                        found.Add("#" + id + " is in two places: host " + a.X + " / " + peer.Name + " " + b.X);
                }
            }
            return found;
        }

        // The differences on one line, for a failure message.
        public string Summary()
        {
            List<string> found = Differences();
            return found.Count + " differences: " + string.Join(" | ", found.Take(6));
        }

        public string Report()
        {
            var text = new StringBuilder();
            foreach (string line in Differences())
                text.AppendLine(line);
            return text.ToString();
        }
    }
}
