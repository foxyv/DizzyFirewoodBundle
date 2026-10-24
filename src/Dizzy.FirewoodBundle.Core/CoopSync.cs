using System;
using System.IO;

namespace Dizzy.FirewoodBundle
{
    // What one bundle sends the other players. Everything a bundle has that Sailwind Coop
    // does not carry itself: the count and layout (Stack), the food state, amount and health.
    public struct BundleState
    {
        public int Id;
        public float Amount;
        public float Health;
        public bool HasFood;
        public float Dried;
        public float Smoked;
        public float Salted;
        public float Spoiled;
        public float Stack;
    }

    // The packet that carries a BundleState. A guest sends it to the host, and the host
    // passes it on.
    public static class BundleWire
    {
        // Raise this when the layout below changes. A player on another version is ignored.
        public const byte Protocol = 1;

        public static void Write(BinaryWriter writer, BundleState state)
        {
            writer.Write(Protocol);
            writer.Write(state.Id);
            writer.Write(state.Amount);
            writer.Write(state.Health);
            writer.Write(state.HasFood);
            writer.Write(state.Dried);
            writer.Write(state.Smoked);
            writer.Write(state.Salted);
            writer.Write(state.Spoiled);
            writer.Write(state.Stack);
        }

        // False when the packet comes from another version. Nothing else is read then.
        public static bool TryRead(BinaryReader reader, out BundleState state)
        {
            state = new BundleState();
            if (reader.ReadByte() != Protocol)
                return false;

            state.Id = reader.ReadInt32();
            state.Amount = reader.ReadSingle();
            state.Health = reader.ReadSingle();
            state.HasFood = reader.ReadBoolean();
            state.Dried = reader.ReadSingle();
            state.Smoked = reader.ReadSingle();
            state.Salted = reader.ReadSingle();
            state.Spoiled = reader.ReadSingle();
            state.Stack = reader.ReadSingle();
            return true;
        }
    }

    // A stack's count, width and color in the one number the game saves it as.
    public struct StackCode
    {
        public const int WidthStride = 1000;
        public const int ColorStride = 10000;

        public int Count;
        public int Width;
        public int Color;

        public static float Encode(int count, int width, int color)
        {
            return count + width * WidthStride + color * ColorStride;
        }

        public static StackCode Decode(float saved)
        {
            int value = (int)Math.Round(saved);
            var code = new StackCode();
            code.Color = value / ColorStride;
            value %= ColorStride;
            code.Count = value % WidthStride;
            code.Width = value / WidthStride;
            return code;
        }

        // A single piece that is not on a hook. It carries no stack state at all.
        public bool IsSingle(int hangWidth)
        {
            return Count <= 1 && Width != hangWidth;
        }
    }

    // The decisions that keep two players' bundles the same. The game code asks these and
    // does what they say, so the same rules can be run without the game.
    public static class SyncRules
    {
        // Coop's packet type is a byte and it uses less than 230 of them. Dizzy Clothesline
        // takes 241 to 248, so bundles look from 249 up.
        public static readonly byte[] PacketTypeCandidates = { 249, 250, 251, 252, 253, 254 };

        // The first candidate that Coop has not given a meaning, or null when none is free.
        public static byte? PickPacketType(Func<byte, bool> isDefined)
        {
            for (int i = 0; i < PacketTypeCandidates.Length; i++)
            {
                if (!isDefined(PacketTypeCandidates[i]))
                    return PacketTypeCandidates[i];
            }
            return null;
        }

        // A bundle changed. It is sent later, unless we are applying another player's change
        // (which would echo it back) or loading a save (which would flood everyone on a join).
        public static bool ShouldNote(bool present, bool applyingRemote, bool loading)
        {
            return present && !applyingRemote && !loading;
        }

        // Changed bundles go out at the end of the frame, in a session, unless Coop is
        // applying another player's state and what changed is only the result of that.
        public static bool ShouldSend(bool multiplayer, bool coopApplyingRemote)
        {
            return multiplayer && !coopApplyingRemote;
        }

        // The host passes a guest's bundle on to everyone else. A guest sends to the host only.
        public static bool ShouldRelay(bool isHost)
        {
            return isHost;
        }

        // Another player carries the item. Coop freezes such an item by setting held to the
        // local player's pointer, so held alone can't tell. heldBySomeoneElse is "held is set
        // and does not hold this item". inRemoteHeld is "Coop lists it among remote-held items".
        public static bool HeldByOther(bool present, bool heldBySomeoneElse, bool multiplayer, bool inRemoteHeld)
        {
            if (!present)
                return false;
            if (heldBySomeoneElse)
                return true;
            return multiplayer && inRemoteHeld;
        }

        // A new piece must be announced to Coop before anyone picks it up: a pick-up of an
        // item Coop does not know takes the nearest item of the same prefab, often the bundle.
        public static bool ShouldAnnounceSpawn(bool present, bool applyingRemote, bool multiplayer)
        {
            return present && !applyingRemote && multiplayer;
        }
    }
}
