using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dizzy.FirewoodBundle.Tests.CoopSim
{
    // Where a bundle keeps its count. Candles keep it in amount, which Coop sends now and
    // then. Food keeps it in extraValue4 as a StackCode, which Coop never sends.
    public enum SimKind
    {
        Candle,
        Sausage,
    }

    // One item as a peer sees it. Only what the sync rules read or write is here.
    public sealed class SimItem
    {
        public int Id;
        public int Prefab;
        public SimKind Kind;
        public double X;
        public float Amount = 1f;
        public float Health = 1f;
        public float Extra4;
        public float Dried;

        // A candle bundle keeps its count and tie color in amount, like the game: count + color * 1000.
        // A food bundle keeps count, width and color in extraValue4.
        public int Count
        {
            get
            {
                if (Kind == SimKind.Candle)
                    return Math.Max(1, (int)Amount % 1000);
                return Math.Max(1, StackCode.Decode(Extra4).Count);
            }
        }

        public int Color
        {
            get { return Kind == SimKind.Candle ? (int)Amount / 1000 : StackCode.Decode(Extra4).Color; }
        }

        public int Width
        {
            get { return Kind == SimKind.Candle ? 0 : StackCode.Decode(Extra4).Width; }
        }

        public SimItem Clone()
        {
            return (SimItem)MemberwiseClone();
        }

        // A food bundle that hangs from a hook. It stays a bundle at a count of one.
        public bool Hung
        {
            get { return Kind == SimKind.Sausage && StackCode.Decode(Extra4).Width == HangWidth; }
        }

        public const int HangWidth = 8;

        public void SetCount(int count)
        {
            if (Kind == SimKind.Candle)
            {
                Amount = count <= 1 ? 1 : count + Color * 1000;
                return;
            }
            int width = Hung ? HangWidth : Width;
            Extra4 = count <= 1 && width != HangWidth ? 0f : StackCode.Encode(count, width, Color);
        }

        // The tie color of a bundle. A single has none.
        public void SetColor(int color)
        {
            if (Count <= 1)
                return;
            if (Kind == SimKind.Candle)
                Amount = Count + color * 1000;
            else
                Extra4 = StackCode.Encode(Count, Width, color);
        }

        // The width a stack of food is drawn at. Candles have none.
        public void SetWidth(int width)
        {
            if (Kind == SimKind.Sausage && Count > 1)
                Extra4 = StackCode.Encode(Count, width, Color);
        }

        public void SetHung(bool hung)
        {
            if (Kind != SimKind.Sausage)
                return;
            int count = Count;
            Extra4 = hung ? StackCode.Encode(count, HangWidth, Color) : (count <= 1 ? 0f : StackCode.Encode(count, 0, Color));
        }

        public override string ToString()
        {
            return "#" + Id + " prefab " + Prefab + " x" + Count + " health " + Health + " dried " + Dried + " at " + X;
        }
    }

    // What Coop and Bundle Up do, as switches. The defaults are how the game works now.
    // Turning one off shows what depends on it.
    public sealed class SimRules
    {
        // Coop. These are copied from how Coop behaves, not choices of ours.
        public bool CoopSetsHeldPointer = true;       // C5
        public bool CoopTakesNearestOnUnknownId = true; // C6
        public bool HostSettlesHealth = true;         // J-031
        public bool SettleOnlyAfterAmount = true;     // J-037: not after the health packet, which has the old amount
        public bool RememberRemoteDestroys = true;    // J-030
        public bool HostPacketsStand = true;          // J-033
        public bool IgnoreCountSetByCoop = true;      // J-032
        public bool CoopDropsLeaversItems = true;     // C: on a player leaving, their items drop where they were
        public bool CoopArbitratesPickUp = true;      // Coop 0.4.0: a guest asks the host, which approves or denies
        public bool HostArbitrates = true;            // J-029
        public bool HostPassesOnUnknown = true;       // J-038: a bundle packet for a bundle the host lacks
        public bool CoopHostRelays = true;            // C4

        // Bundle Up.
        public bool SendBundlePacket = true;          // packet 249
        public bool AnnounceSpawn = true;             // Spawned() after RegisterToSave
        public bool RefuseWhenHeldByOther = true;     // combine, take from, gather
        public bool SuppressEchoes = true;            // no send for a rebuild another player's change caused
        public bool MergeConcurrentCounts = true;     // X1: add the other player's change to ours
        public bool IgnorePickUpOfDestroyed = true;   // X2: a pick-up of a bundle just combined away
        public bool KeepPendingChange = true;         // J-036: a settled packet before our end of frame
    }

    public sealed class SimMessage
    {
        public string From;
        public string Holder;        // who picked it up, on a PickedUp
        public string Hop;           // who sent it over this link: the host, for a relayed message
        public string To;
        public string Type;
        public byte[] Body;          // the bundle packet, as BundleWire writes it
        public List<SimItem> Items;  // Coop's own messages carry items
        public int Id;
        public double X;
    }
}
