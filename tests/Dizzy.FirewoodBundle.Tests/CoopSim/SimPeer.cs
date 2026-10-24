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
        private int _nextLocalId;
        private bool _applying;

        public readonly string Name;
        public readonly bool IsHost;
        public readonly Dictionary<int, SimItem> Items = new Dictionary<int, SimItem>();
        public readonly HashSet<int> RemoteHeld = new HashSet<int>();
        public readonly HashSet<int> HeldByMe = new HashSet<int>();

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

        public void PickUp(SimItem item)
        {
            HeldByMe.Add(item.Id);
            _net.Send(this, "PickedUp", new SimMessage { Id = item.Id, X = item.X, Items = new List<SimItem> { item.Clone() } });
        }

        public void Drop(SimItem item, double x)
        {
            HeldByMe.Remove(item.Id);
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
            item.SetCount(count);
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
            into.SetCount(into.Count + from.Count);
            Items.Remove(from.Id);
            HeldByMe.Remove(from.Id);
            _net.Send(this, "Destroyed", new SimMessage { Id = from.Id });
            Note(into);
            return true;
        }

        // The item is used up, such as a last piece eaten.
        public void Destroy(SimItem item)
        {
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
                };
                var stream = new MemoryStream();
                BundleWire.Write(new BinaryWriter(stream), state);
                _net.Send(this, "Bundle", new SimMessage { Body = stream.ToArray() });
            }
            _changed.Clear();
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
                    ReceivePickUp(message);
                    break;

                case "Dropped":
                    RemoteHeld.Remove(message.Id);
                    SimItem dropped;
                    if (Items.TryGetValue(message.Id, out dropped))
                    {
                        dropped.X = message.X;
                        Rebuild(dropped);
                    }
                    break;

                case "Destroyed":
                    Items.Remove(message.Id);
                    RemoteHeld.Remove(message.Id);
                    HeldByMe.Remove(message.Id);
                    break;

                case "Bundle":
                    ReceiveBundle(message);
                    break;
            }
        }

        private void ReceivePickUp(SimMessage message)
        {
            SimItem item;
            if (!Items.TryGetValue(message.Id, out item))
            {
                // Coop does not know this item. It takes the nearest of the same prefab and
                // gives it the new ID.
                SimItem sent = message.Items[0];
                if (!Rules.CoopTakesNearestOnUnknownId)
                    return;
                item = Items.Values
                    .Where(i => i.Prefab == sent.Prefab && !RemoteHeld.Contains(i.Id) && !HeldByMe.Contains(i.Id))
                    .OrderBy(i => Math.Abs(i.X - message.X))
                    .ThenBy(i => i.Id)
                    .FirstOrDefault();
                if (item == null)
                    return;
                Items.Remove(item.Id);
                item.Id = message.Id;
                Items[item.Id] = item;
            }
            RemoteHeld.Add(message.Id);
        }

        private void ReceiveBundle(SimMessage message)
        {
            BundleState state;
            if (!BundleWire.TryRead(new BinaryReader(new MemoryStream(message.Body)), out state))
                return;
            SimItem item;
            if (!Items.TryGetValue(state.Id, out item))
                return;

            _applying = true;
            try
            {
                item.Amount = state.Amount;
                item.Health = state.Health;
                if (state.HasFood)
                    item.Dried = state.Dried;
                if (item.Kind == SimKind.Sausage)
                    item.Extra4 = state.Stack;
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
