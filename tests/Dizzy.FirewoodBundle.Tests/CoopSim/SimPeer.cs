using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    // One player's copy of the world, running Bundle Up's rules (the real ones from Core)
    // on top of Coop's behaviour.
    public sealed class SimPeer
    {
        private readonly SimNetwork _net;
        private readonly HashSet<SimItem> _changed = new HashSet<SimItem>();
        private readonly Dictionary<int, int> _bases = new Dictionary<int, int>();
        private readonly DestroyedMemory _destroyed = new DestroyedMemory();
        private readonly Dictionary<int, string> _holders = new Dictionary<int, string>();   // the host's record of who holds what
        private int _nextLocalId;
        private bool _applying;

        public readonly string Name;
        public readonly bool IsHost;
        public readonly Dictionary<int, SimItem> Items = new Dictionary<int, SimItem>();
        public readonly HashSet<int> RemoteHeld = new HashSet<int>();
        public readonly HashSet<int> HeldByMe = new HashSet<int>();

        // Bundle packets that arrived here, to see that none came twice.
        public int BundlePacketsReceived;

        // Bundle packets that this player sent and that came back to them.
        public int OwnPacketsReceived;

        // True while a save is loading, as CoopBridge.Loading is.
        public bool Loading;

        internal SimPeer(SimNetwork net, string name, bool isHost, int index)
        {
            _net = net;
            Name = name;
            IsHost = isHost;
            _nextLocalId = (index + 1) * 1000000;
        }

        private SimRules Rules { get { return _net.Rules; } }

        private bool Multiplayer { get { return _net.Peers.Count > 1; } }

        // ---- What the game does ----

        // An item the game makes, such as one bought. Coop announces it on its own.
        public SimItem Create(int prefab, SimKind kind, double x, int count = 1)
        {
            var item = new SimItem { Id = _nextLocalId++, Prefab = prefab, Kind = kind, X = x };
            item.SetCount(count);
            Items[item.Id] = item;
            _net.Send(this, "Spawned", new SimMessage { Items = new List<SimItem> { item.Clone() } });
            return item;
        }

        // The host takes it at once and tells everyone. A guest takes it in hand and asks the
        // host, which approves or denies (Coop 0.4.0).
        public void PickUp(SimItem item)
        {
            HeldByMe.Add(item.Id);
            bool ask = !IsHost && Rules.CoopArbitratesPickUp;
            if (IsHost)
                _holders[item.Id] = Name;
            _net.Send(this, ask ? "PickRequest" : "PickedUp", new SimMessage { Id = item.Id, X = item.X, Holder = Name, Items = new List<SimItem> { item.Clone() } });
        }

        public void Drop(SimItem item, double x)
        {
            HeldByMe.Remove(item.Id);
            _holders.Remove(item.Id);
            item.X = x;
            _net.Send(this, "Dropped", new SimMessage { Id = item.Id, X = x });
        }

        // Coop sends health with amount when health changes. This is the only time it sends amount.
        public void ChangeHealth(SimItem item, float health)
        {
            item.Health = health;
            _net.Send(this, "ItemUpdate", new SimMessage { Items = new List<SimItem> { item.Clone() } });
        }

        // ---- What Bundle Up does ----

        public bool HeldByOther(SimItem item)
        {
            bool pointer = Rules.CoopSetsHeldPointer && RemoteHeld.Contains(item.Id);
            return SyncRules.HeldByOther(true, pointer, Multiplayer, RemoteHeld.Contains(item.Id));
        }

        // Makes the item a bundle of count. Returns false when it is refused.
        public bool SetBundle(SimItem item, int count)
        {
            if (Rules.RefuseWhenHeldByOther && HeldByOther(item))
                return false;
            CountWillChange(item);
            item.SetCount(count);
            Note(item);
            return true;
        }

        private void CountWillChange(SimItem item)
        {
            if (!_applying && !Loading && !_bases.ContainsKey(item.Id))
                _bases[item.Id] = item.Count;
        }

        // The tie color key, on the bundle in your hand or the one you look at.
        public bool Recolor(SimItem item, int color)
        {
            if (Rules.RefuseWhenHeldByOther && HeldByOther(item))
                return false;
            item.SetColor(color);
            Note(item);
            return true;
        }

        // The width key, on a stack of food.
        public bool CycleWidth(SimItem item, int width)
        {
            if (Rules.RefuseWhenHeldByOther && HeldByOther(item))
                return false;
            item.SetWidth(width);
            Note(item);
            return true;
        }

        // Takes one piece off a bundle: a new item, a count of one less on the bundle.
        public SimItem TakeOne(SimItem bundle)
        {
            if (bundle.Count <= 1)
                return null;
            if (Rules.RefuseWhenHeldByOther && HeldByOther(bundle))
                return null;

            SimItem piece = SpawnPiece(bundle.Prefab, bundle.Kind, bundle.X, bundle.Health, bundle.Dried);

            CountWillChange(bundle);
            bundle.SetCount(bundle.Count - 1);
            Note(bundle);
            return piece;
        }

        // A new loose piece made by Bundle Up, not by the game: one taken off a bundle, a log
        // from a stove, a hook off a line. Coop must be told about it.
        public SimItem SpawnPiece(int prefab, SimKind kind, double x, float health = 1f, float dried = 0f)
        {
            var piece = new SimItem { Id = _nextLocalId++, Prefab = prefab, Kind = kind, X = x, Health = health, Dried = dried };
            Items[piece.Id] = piece;
            if (SyncRules.ShouldAnnounceSpawn(Rules.AnnounceSpawn, _applying, Multiplayer))
                _net.Send(this, "Spawned", new SimMessage { Items = new List<SimItem> { piece.Clone() } });
            return piece;
        }

        // Puts one bundle into another. Refused if either is held by another player.
        public bool Combine(SimItem into, SimItem from)
        {
            if (Rules.RefuseWhenHeldByOther && (HeldByOther(into) || HeldByOther(from)))
                return false;
            CountWillChange(into);
            into.SetCount(into.Count + from.Count);
            _destroyed.Remember(from.Id, 0f);
            Items.Remove(from.Id);
            HeldByMe.Remove(from.Id);
            _net.Send(this, "Destroyed", new SimMessage { Id = from.Id });
            Note(into);
            return true;
        }

        // The item is used up, such as a last piece eaten.
        public void Destroy(SimItem item)
        {
            _destroyed.Remember(item.Id, 0f);
            Items.Remove(item.Id);
            HeldByMe.Remove(item.Id);
            _net.Send(this, "Destroyed", new SimMessage { Id = item.Id });
        }

        // Hangs a food bundle on a hook, or takes it off.
        public bool Hang(SimItem item, bool hung)
        {
            if (Rules.RefuseWhenHeldByOther && HeldByOther(item))
                return false;
            item.SetHung(hung);
            Note(item);
            return true;
        }

        // The game loads a save. Every bundle is rebuilt, and none of that is sent.
        public void Reload()
        {
            Loading = true;
            try
            {
                foreach (SimItem item in Items.Values)
                    Note(item);
            }
            finally
            {
                Loading = false;
            }
        }

        private void Note(SimItem item)
        {
            if (SyncRules.ShouldNote(Rules.SendBundlePacket, _applying && Rules.SuppressEchoes, Loading))
                _changed.Add(item);
        }

        // The end of a frame: changed bundles go out once each.
        public void EndFrame()
        {
            if (_changed.Count == 0)
                return;
            foreach (SimItem item in _changed.OrderBy(i => i.Id))
            {
                if (!SyncRules.ShouldSend(Multiplayer, false))
                    continue;
                var state = new BundleState
                {
                    Id = item.Id,
                    Amount = item.Amount,
                    Health = item.Health,
                    HasFood = item.Kind == SimKind.Sausage,
                    Dried = item.Dried,
                    Stack = item.Extra4,
                    Count = item.Count,
                    BaseCount = _bases.ContainsKey(item.Id) ? _bases[item.Id] : item.Count,
                };
                var stream = new MemoryStream();
                BundleWire.Write(new BinaryWriter(stream), state);
                _net.Send(this, "Bundle", new SimMessage { Body = stream.ToArray() });
            }
            _changed.Clear();
            _bases.Clear();
        }

        // ---- Arriving messages ----

        internal void Receive(SimMessage message)
        {
            switch (message.Type)
            {
                case "Snapshot":
                    Items.Clear();
                    foreach (SimItem item in message.Items)
                        Items[item.Id] = item.Clone();
                    break;

                case "Spawned":
                    foreach (SimItem item in message.Items)
                        if (!Items.ContainsKey(item.Id))
                            Items[item.Id] = item.Clone();
                    break;

                case "ItemUpdate":
                    // Coop sets amount and health, and rebuilds nothing.
                    foreach (SimItem sent in message.Items)
                    {
                        SimItem here;
                        if (Items.TryGetValue(sent.Id, out here))
                        {
                            here.Health = sent.Health;
                            here.Amount = sent.Amount;
                        }
                    }
                    break;

                case "PickedUp":
                    // The host's approval of our own request changes nothing here.
                    if (message.Holder != Name)
                        ReceivePickUp(message);
                    break;

                case "PickRequest":
                    ReceivePickRequest(message);
                    break;

                case "Denied":
                    // Coop makes the guest drop it, without telling anyone.
                    HeldByMe.Remove(message.Id);
                    break;

                case "Dropped":
                    RemoteHeld.Remove(message.Id);
                    _holders.Remove(message.Id);
                    SimItem dropped;
                    if (Items.TryGetValue(message.Id, out dropped))
                    {
                        dropped.X = message.X;
                        Rebuild(dropped);
                    }
                    break;

                case "Destroyed":
                    if (Rules.RememberRemoteDestroys)
                        _destroyed.Remember(message.Id, 0f);
                    Items.Remove(message.Id);
                    RemoteHeld.Remove(message.Id);
                    HeldByMe.Remove(message.Id);
                    break;

                case "Bundle":
                    ReceiveBundle(message);
                    break;
            }
        }

        // The host's reply to a guest's bundle packet: what the bundle is here now.
        internal void Answer(SimMessage message)
        {
            BundleState received;
            if (!BundleWire.TryRead(new BinaryReader(new MemoryStream(message.Body)), out received))
                return;
            Answer(received.Id);
        }

        internal void Answer(int id)
        {
            SimItem item;
            if (!Items.TryGetValue(id, out item))
                return;
            var state = new BundleState
            {
                Id = item.Id,
                Amount = item.Amount,
                Health = item.Health,
                HasFood = item.Kind == SimKind.Sausage,
                Dried = item.Dried,
                Stack = item.Extra4,
                Count = item.Count,
                BaseCount = CountMerge.Settled,
            };
            var stream = new MemoryStream();
            BundleWire.Write(new BinaryWriter(stream), state);
            _net.Send(this, "Bundle", new SimMessage { Body = stream.ToArray() });
        }

        // The host: approve a guest's pick-up unless someone else holds the item.
        private void ReceivePickRequest(SimMessage message)
        {
            string holder;
            if (_holders.TryGetValue(message.Id, out holder) && holder != message.Holder)
            {
                _net.SendTo(this, message.Holder, "Denied", new SimMessage { Id = message.Id });
                return;
            }
            if (!ReceivePickUp(message))
            {
                _net.SendTo(this, message.Holder, "Denied", new SimMessage { Id = message.Id });
                return;
            }
            _holders[message.Id] = message.Holder;
            _net.Send(this, "PickedUp", new SimMessage { Id = message.Id, X = message.X, Holder = message.Holder, Items = message.Items });
        }

        private bool ReceivePickUp(SimMessage message)
        {
            SimItem item;
            if (!Items.TryGetValue(message.Id, out item))
            {
                // Coop does not know this item. It takes the nearest of the same prefab and
                // gives it the new ID.
                SimItem sent = message.Items[0];
                if (!Rules.CoopTakesNearestOnUnknownId)
                    return false;
                if (Rules.IgnorePickUpOfDestroyed && _destroyed.WasDestroyed(message.Id, 0f))
                    return false;
                item = Items.Values
                    .Where(i => i.Prefab == sent.Prefab && !RemoteHeld.Contains(i.Id) && !HeldByMe.Contains(i.Id))
                    .OrderBy(i => Math.Abs(i.X - message.X))
                    .ThenBy(i => i.Id)
                    .FirstOrDefault();
                if (item == null)
                    return false;
                Items.Remove(item.Id);
                item.Id = message.Id;
                Items[item.Id] = item;
            }
            RemoteHeld.Add(message.Id);
            return true;
        }

        private void ReceiveBundle(SimMessage message)
        {
            BundlePacketsReceived++;
            if (message.From == Name)
                OwnPacketsReceived++;
            BundleState state;
            if (!BundleWire.TryRead(new BinaryReader(new MemoryStream(message.Body)), out state))
                return;
            SimItem item;
            if (!Items.TryGetValue(state.Id, out item))
                return;

            int local = item.Count;
            _applying = true;
            try
            {
                item.Amount = state.Amount;
                item.Health = state.Health;
                if (state.HasFood)
                    item.Dried = state.Dried;
                if (item.Kind == SimKind.Sausage)
                    item.Extra4 = state.Stack;
                int? merged = Rules.MergeConcurrentCounts ? CountMerge.Resolve(local, state.BaseCount, state.Count) : null;
                if (merged.HasValue)
                    item.SetCount(merged.Value);
                Rebuild(item);
            }
            finally
            {
                _applying = false;
            }
        }

        // Applying another player's change redraws the bundle, and a redraw is a change.
        // It must not go back out.
        private void Rebuild(SimItem item)
        {
            bool was = _applying;
            _applying = true;
            try
            {
                Note(item);
            }
            finally
            {
                _applying = was;
            }
        }
    }
}
