using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // Shares bundles with the other players through Sailwind Coop.
    //
    // Coop sends an item's saved state only when a player joins. After that it sends
    // amount now and then and never the spare extra value food bundles live in, and it
    // never redraws anything. So whenever this mod rebuilds a bundle, it sends the
    // item's whole state on a packet type of its own, and the other players rebuild it
    // the same way. Coop's packet type is a byte and it uses less than 230 of them.
    // Dizzy Clothesline takes 241 to 248, so bundles look from 249 up.
    //
    // A guest sends to the host, and the host passes it on. Everything is found by
    // reflection: without Coop loaded, none of this runs.
    internal static partial class CoopBridge
    {
        internal const string CoopGuid = "com.sailwindcoop.mod";

        private static readonly HashSet<ShipItem> Changed = new HashSet<ShipItem>();
        private static readonly Dictionary<ShipItem, int> Bases = new Dictionary<ShipItem, int>();

        // The count Coop's own amount packet gave a bundle. A candle bundle keeps its count in
        // amount, so a health change on another player's side sets it here without any change
        // made here (J-032).
        private static readonly Dictionary<ShipItem, int> SetByCoop = new Dictionary<ShipItem, int>();
        private static readonly DestroyedMemory Destroyed_ = new DestroyedMemory();
        private static bool _present;
        private static bool _applying;
        private static bool _registering;
        private static bool _warnedProtocol;
        private static bool _warnedTaken;
        private static object _registeredOn;

        private static PropertyInfo _isMultiplayer;
        private static PropertyInfo _isHost;
        private static PropertyInfo _network;
        private static FieldInfo _handlers;
        private static MethodInfo _register;
        private static MethodInfo _sendToAll;
        private static MethodInfo _sendToAllExcept;
        private static PropertyInfo _itemSync;
        private static PropertyInfo _remoteApplying;
        private static MethodInfo _setRemoteApplying;
        private static MethodInfo _find;
        private static MethodInfo _findInactive;
        private static FieldInfo _remoteHeld;
        private static MethodInfo _spawned;
        private static object _packetType;
        private static Delegate _handler;

        // Set while a saved bundle loads, so a join does not send every bundle back out.
        internal static bool Loading;

        internal static void Apply(Harmony harmony)
        {
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(CoopGuid, out info) || info.Instance == null)
                return;

            try
            {
                Type plugin = info.Instance.GetType();
                Assembly coop = plugin.Assembly;
                Type network = coop.GetType("SailwindCoop.Networking.P2PNetworkManager");
                Type packetType = coop.GetType("SailwindCoop.Networking.Packets.PacketType");
                Type itemSync = coop.GetType("SailwindCoop.Sync.ItemSyncManager");
                if (network == null || packetType == null || itemSync == null)
                {
                    Plugin.Log.LogWarning("Sailwind Coop is loaded, but its networking was not found. Bundles will not be shared with other players.");
                    return;
                }

                const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
                _isMultiplayer = plugin.GetProperty("IsMultiplayer", PublicStatic);
                _isHost = plugin.GetProperty("IsHost", PublicStatic);
                _network = plugin.GetProperty("NetworkManager", PublicStatic);
                _handlers = network.GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Instance);
                _register = network.GetMethod("RegisterHandler");
                Type handlerType = _register != null ? _register.GetParameters()[1].ParameterType : null;
                Type steamId = handlerType != null && handlerType.IsGenericType ? handlerType.GetGenericArguments()[0] : null;
                if (steamId == null)
                {
                    Plugin.Log.LogWarning("This version of Sailwind Coop has changed its networking. Bundles will not be shared with other players.");
                    return;
                }

                _sendToAll = network.GetMethod("SendToAllReliable", new[] { packetType, typeof(Action<BinaryWriter>) });
                _sendToAllExcept = network.GetMethod("SendToAllExcept", new[] { steamId, packetType, typeof(Action<BinaryWriter>), typeof(bool) });
                _itemSync = itemSync.GetProperty("Instance", PublicStatic);
                _remoteApplying = itemSync.GetProperty("IsApplyingRemoteState", BindingFlags.Public | BindingFlags.Instance);
                _setRemoteApplying = _remoteApplying != null ? _remoteApplying.GetSetMethod(true) : null;
                _find = itemSync.GetMethod("FindItemByInstanceId", PublicStatic, null, new[] { typeof(int) }, null);
                _findInactive = itemSync.GetMethod("FindInactiveItemByInstanceId", PublicStatic, null, new[] { typeof(int) }, null);
                _remoteHeld = itemSync.GetField("_remoteHeldItems", BindingFlags.NonPublic | BindingFlags.Instance);
                _spawned = itemSync.GetMethod("OnLocalItemSpawned", new[] { typeof(ShipItem) });
                if (_spawned == null)
                    Plugin.Log.LogWarning("Could not find how Coop learns about new items. Pieces taken off a bundle may not reach other players.");
                if (_remoteHeld == null)
                    Plugin.Log.LogWarning("Could not find which items other players hold. Their held bundles may still offer to combine.");

                if (_isMultiplayer == null || _isHost == null || _network == null || _handlers == null || _sendToAll == null
                    || _sendToAllExcept == null || _itemSync == null || _remoteApplying == null || _setRemoteApplying == null
                    || _find == null)
                {
                    Plugin.Log.LogWarning("This version of Sailwind Coop has changed its networking. Bundles will not be shared with other players.");
                    return;
                }

                _packetType = PickPacketType(packetType);
                if (_packetType == null)
                {
                    Plugin.Log.LogWarning("Sailwind Coop has no free packet type left. Bundles will not be shared with other players.");
                    return;
                }

                MethodInfo receive = typeof(CoopBridge).GetMethod(nameof(Receive), BindingFlags.NonPublic | BindingFlags.Static);
                _handler = Delegate.CreateDelegate(handlerType, receive.MakeGenericMethod(steamId));

                // Coop registers its handlers each time a session starts, and clears them
                // when it ends. Register ours alongside.
                harmony.Patch(_register, postfix: new HarmonyMethod(typeof(CoopBridge), nameof(AfterRegisterHandler)));
                Trace(harmony, itemSync, "OnRemoteItemDropped", "dropped");
                Trace(harmony, itemSync, "OnRemoteItemPickedUp", "picked up");

                // A pick-up of an item that is gone takes the nearest of its prefab (J-007), so
                // every destroy, ours or another player's, is remembered for a while (J-030).
                MethodInfo amount = AccessTools.Method(itemSync, "OnRemoteItemAmountChanged");
                if (amount != null)
                    harmony.Patch(amount, postfix: new HarmonyMethod(typeof(CoopBridge), nameof(RememberCountSetByCoop)));
                // Coop sends every health change as a health packet and then an amount packet, in
                // that order. Settling after the health packet would answer with the old amount,
                // so only the amount packet settles (J-037).
                if (amount != null)
                    harmony.Patch(amount, postfix: new HarmonyMethod(typeof(CoopBridge), nameof(SettleAfterCoopUpdate)));
                foreach (string name in new[] { "OnRemoteItemDestroyed", "OnLocalItemDestroyed" })
                {
                    MethodInfo destroyed = AccessTools.Method(itemSync, name);
                    if (destroyed != null)
                        harmony.Patch(destroyed, prefix: new HarmonyMethod(typeof(CoopBridge), nameof(RememberDestroyedItem)));
                }

                _present = true;
                EnsureHandler(_network.GetValue(null, null));
                Plugin.Log.LogInfo("Sailwind Coop found. Bundles are shared with other players (packet type " + Convert.ToByte(_packetType) + ").");
            }
            catch (Exception ex)
            {
                _present = false;
                Plugin.Log.LogWarning("Could not link to Sailwind Coop. Bundles will not be shared with other players: " + ex);
            }
        }

        private static readonly Dictionary<MethodBase, string> Traced = new Dictionary<MethodBase, string>();

        private static void Trace(Harmony harmony, Type type, string method, string what)
        {
            MethodInfo target = AccessTools.Method(type, method);
            if (target == null)
                return;
            Traced[target] = what;
            harmony.Patch(
                target,
                prefix: new HarmonyMethod(typeof(CoopBridge), nameof(TraceBefore)),
                postfix: new HarmonyMethod(typeof(CoopBridge), nameof(TraceAfter)));
        }

        // Says which bundle another player picked up or dropped, before and after Coop
        // applies it. Only bundles are named, so the log stays short.
        private static bool TraceBefore(MethodBase __originalMethod, object[] __args, out ShipItem __state)
        {
            if (Traced[__originalMethod] == "picked up")
            {
                int id = ItemId(__args);
                if (id != 0 && Destroyed_.WasDestroyed(id, Time.realtimeSinceStartup))
                {
                    Plugin.Log.LogInfo("[Coop] Another player picked up " + id + ", a bundle we just combined. Ignored, so no other bundle takes its ID.");
                    __state = null;
                    return false;
                }
            }

            __state = TracedItem(__args);
            if (__state != null)
            {
                Stall.Mark("Coop applying: another player " + Traced[__originalMethod] + " " + __state.name);
                Stall.Bundle("another player " + Traced[__originalMethod] + " " + __state.name + " (" + FirewoodPieces.CountOf(__state) + ")");
            }
            if (__state != null)
                Plugin.Log.LogInfo("[Coop] Another player " + Traced[__originalMethod] + " " + __state.name + " (" + FirewoodPieces.CountOf(__state) + "). Applying...");
            return true;
        }

        private static void RememberDestroyedItem(object[] __args)
        {
            int id = __args != null && __args.Length > 0 && __args[0] is int ? (int)__args[0] : ItemId(__args);
            if (id != 0)
                Destroyed_.Remember(id, Time.realtimeSinceStartup);
        }

        private static int ItemId(object[] args)
        {
            try
            {
                if (args == null || args.Length == 0 || args[0] == null)
                    return 0;
                FieldInfo field = args[0].GetType().GetField("ItemInstanceId");
                return field != null ? (int)field.GetValue(args[0]) : 0;
            }
            catch
            {
                return 0;
            }
        }

        // A bundle was used up here (combined into another). Called before it is destroyed.
        internal static void Destroyed(ShipItem item)
        {
            if (!_present || item == null)
                return;
            SaveablePrefab save = item.GetComponent<SaveablePrefab>();
            if (save != null && save.instanceId != 0)
                Destroyed_.Remember(save.instanceId, Time.realtimeSinceStartup);
        }

        // About to change a bundle's count. The count it had is what the other players are
        // told it changed from. The first change in a frame sets it.
        internal static void CountWillChange(ShipItem item)
        {
            if (!_present || _applying || Loading || item == null)
                return;
            SetByCoop.Remove(item);
            if (Bases.ContainsKey(item))
                return;
            Bases[item] = CountNow(item);
        }

        private static int CountNow(ShipItem item)
        {
            return HookLinePieces.IsHook(item) ? HookLinePieces.CountOf(item) : FirewoodPieces.CountOf(item);
        }

        private static void TraceAfter(MethodBase __originalMethod, ShipItem __state)
        {
            if (__state != null)
                Stall.Mark("Coop done: another player " + Traced[__originalMethod] + " " + __state.name);
            if (__state != null)
                Plugin.Log.LogInfo("[Coop] ...applied: " + Traced[__originalMethod] + " " + __state.name + ".");
        }

        private static ShipItem TracedItem(object[] args)
        {
            try
            {
                if (args == null || args.Length == 0 || args[0] == null)
                    return null;
                FieldInfo field = args[0].GetType().GetField("ItemInstanceId");
                if (field == null)
                    return null;
                ShipItem item = Find((int)field.GetValue(args[0]));
                return item != null && (FirewoodPieces.KindOf(item) != null || HookLinePieces.IsHook(item)) ? item : null;
            }
            catch
            {
                return null;
            }
        }

        private static object PickPacketType(Type enumType)
        {
            byte? free = SyncRules.PickPacketType(candidate => Enum.IsDefined(enumType, candidate));
            return free.HasValue ? Enum.ToObject(enumType, free.Value) : null;
        }

        private static void AfterRegisterHandler(object __instance)
        {
            if (!_registering)
                EnsureHandler(__instance);
        }

        private static void EnsureHandler(object network)
        {
            if (!_present || network == null || _registering)
                return;

            try
            {
                IDictionary handlers = _handlers.GetValue(network) as IDictionary;
                if (handlers != null && handlers.Contains(_packetType))
                {
                    if (!Equals(handlers[_packetType], _handler) && !_warnedTaken)
                    {
                        _warnedTaken = true;
                        Plugin.Log.LogWarning("Another mod uses Coop packet type " + Convert.ToByte(_packetType) + ". Bundles will not be shared with other players.");
                    }
                    return;
                }
                if (handlers == null && ReferenceEquals(_registeredOn, network))
                    return;

                _registering = true;
                _register.Invoke(network, new object[] { _packetType, _handler });
                _registeredOn = network;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not listen for bundle changes from other players: " + ex);
            }
            finally
            {
                _registering = false;
            }
        }

        // ---- State ----

        private static bool IsMultiplayer
        {
            get { return _present && Get(_isMultiplayer); }
        }

        private static bool IsHost
        {
            get { return _present && Get(_isHost); }
        }

        private static object ItemSync()
        {
            try
            {
                return _itemSync.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private static bool Get(PropertyInfo property, object target = null)
        {
            try
            {
                object value = property.GetValue(target, null);
                return value is bool flag && flag;
            }
            catch
            {
                return false;
            }
        }

        // True while another player carries the item. Coop marks such an item as held by
        // this player's own pointer to freeze its physics, so item.held alone cannot tell.
        // Nothing may combine with it, take from it or gather it until it is put down,
        // or the two games stop agreeing on what it holds.
        internal static bool HeldByOther(ShipItem item)
        {
            if (!_present || item == null)
                return false;
            bool heldBySomeoneElse = item.held != null && !ReferenceEquals(item.held.GetHeldItem(), item);
            bool multiplayer = _remoteHeld != null && IsMultiplayer;
            return SyncRules.HeldByOther(true, heldBySomeoneElse, multiplayer, multiplayer && InRemoteHeld(item));
        }

        private static bool InRemoteHeld(ShipItem item)
        {
            try
            {
                object sync = ItemSync();
                IDictionary held = sync != null ? _remoteHeld.GetValue(sync) as IDictionary : null;
                SaveablePrefab save = item.GetComponent<SaveablePrefab>();
                return held != null && save != null && held.Contains(save.instanceId);
            }
            catch
            {
                return false;
            }
        }

        // A piece taken off a bundle is a new item. Coop must hear of it before anyone
        // picks it up: a pick-up of an item it does not know takes the nearest item of
        // the same kind instead, usually the bundle itself, and gives it the new ID.
        internal static void Spawned(SaveablePrefab saveable)
        {
            if (_spawned == null || saveable == null || !SyncRules.ShouldAnnounceSpawn(_present, _applying, IsMultiplayer))
                return;
            ShipItem item = saveable.GetComponent<ShipItem>();
            if (item == null)
                return;
            try
            {
                object sync = ItemSync();
                if (sync == null)
                    return;
                _spawned.Invoke(sync, new object[] { item });
                Plugin.Log.LogInfo("[Coop] Told the other players about a new " + item.name + " " + saveable.instanceId + ".");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not tell the other players about a new " + item.name + ": " + ex);
            }
        }

        // ---- Sending ----

        // Called whenever a bundle is rebuilt or changes its count. The send waits for
        // the end of the frame, so a change made in steps goes out once.
        internal static void Note(ShipItem item)
        {
            if (item != null && SyncRules.ShouldNote(_present, _applying, Loading))
                Changed.Add(item);
        }

        internal static void Flush()
        {
            if (Changed.Count == 0)
                return;

            try
            {
                object sync = ItemSync();
                if (!SyncRules.ShouldSend(IsMultiplayer, sync != null && Get(_remoteApplying, sync)))
                    return;
                object network = _network.GetValue(null, null);
                if (network == null)
                    return;

                foreach (ShipItem item in Changed)
                {
                    BundleState state;
                    if (item != null && TryRead(item, out state))
                    {
                        _sendToAll.Invoke(network, new object[] { _packetType, Writer(state) });
                        Plugin.Log.LogInfo("[Coop] Sent " + item.name + " " + state.Id + ": " + FirewoodPieces.CountOf(item) + " pieces.");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not send a bundle to the other players: " + ex);
            }
            finally
            {
                Changed.Clear();
                Bases.Clear();
            }
        }

        private static bool TryRead(ShipItem item, out BundleState state)
        {
            state = new BundleState();
            SaveablePrefab save = item.GetComponent<SaveablePrefab>();
            if (save == null || save.instanceId == 0)
                return false;

            state.Id = save.instanceId;
            state.Amount = item.amount;
            state.Health = item.health;
            FoodState food = item.GetComponent<FoodState>();
            if (food != null)
            {
                state.HasFood = true;
                state.Dried = food.dried;
                state.Smoked = food.smoked;
                state.Salted = food.salted;
                state.Spoiled = food.spoiled;
            }
            state.Stack = SausageStacks.Encode(item);
            state.Count = CountNow(item);
            int before;
            state.BaseCount = Bases.TryGetValue(item, out before) ? before : state.Count;
            // The host's packet stands: its settled answer to a guest's change follows it, so
            // there is nothing for a guest to merge. A guest that joined just now already has
            // the change in its snapshot and must not add it again (J-033).
            if (IsHost)
                state.BaseCount = CountMerge.Settled;
            return true;
        }

        private static Action<BinaryWriter> Writer(BundleState state)
        {
            return writer => BundleWire.Write(writer, state);
        }

        // ---- Receiving ----

        // Closed over Coop's SteamId type when the handler is made.
        private static void Receive<TSender>(TSender sender, BinaryReader reader)
        {
            try
            {
                BundleState state;
                if (!BundleWire.TryRead(reader, out state))
                {
                    if (!_warnedProtocol)
                    {
                        _warnedProtocol = true;
                        Plugin.Log.LogWarning("Another player has a different version of " + Plugin.PluginName + ". Their bundle changes are ignored.");
                    }
                    return;
                }

                ShipItem applied = ApplyRemote(state);

                // The host settles it and tells everyone, the sender too, so two changes made at
                // once end the same on every screen. Its own order is the order.
                if (SyncRules.ShouldArbitrate(IsHost) && applied != null)
                    Settle(applied);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not apply a bundle change from another player: " + ex);
            }
        }

        // Sends what the bundle is here now to everyone, the sender of the change included.
        private static void Settle(ShipItem item)
        {
            BundleState settled;
            object network = _network.GetValue(null, null);
            if (network != null && TryRead(item, out settled))
            {
                settled.BaseCount = CountMerge.Settled;
                _sendToAll.Invoke(network, new object[] { _packetType, Writer(settled) });
                Plugin.Log.LogInfo("[Coop] Settled " + item.name + " " + settled.Id + ": " + settled.Count + " pieces.");
            }
        }

        private static void RememberCountSetByCoop(object[] __args)
        {
            try
            {
                int id = ItemId(__args);
                ShipItem item = id != 0 ? Find(id) : null;
                if (item != null && (FirewoodPieces.KindOf(item) != null || HookLinePieces.IsHook(item)))
                    SetByCoop[item] = CountNow(item);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not note a count set by Coop: " + ex);
            }
        }

        // Health and amount travel in Coop's own packets, which the host only passes on. After
        // it has applied the amount, which always follows a health change, it settles the bundle
        // like any other change (J-031, J-037).
        private static void SettleAfterCoopUpdate(object[] __args)
        {
            try
            {
                if (!_present || !SyncRules.ShouldArbitrate(IsHost))
                    return;
                int id = ItemId(__args);
                ShipItem item = id != 0 ? Find(id) : null;
                if (item != null && (FirewoodPieces.KindOf(item) != null || HookLinePieces.IsHook(item)))
                    Settle(item);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not settle a bundle after a health change: " + ex);
            }
        }

        private static ShipItem Find(int id)
        {
            ShipItem item = _find.Invoke(null, new object[] { id }) as ShipItem;
            if (item == null && _findInactive != null)
                item = _findInactive.Invoke(null, new object[] { id }) as ShipItem;
            return item;
        }

        private static ShipItem ApplyRemote(BundleState state)
        {
            ShipItem item = Find(state.Id);
            if (item == null)
            {
                Plugin.Log.LogInfo("[Coop] Received bundle " + state.Id + ", which is not here.");
                return null;
            }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Stall.Mark("applying " + item.name + " from another player");
            Stall.Bundle("applied " + item.name + " from another player");
            Plugin.Log.LogInfo("[Coop] Received " + item.name + " " + state.Id + (item.held != null ? " (held)" : "") + ".");
            try
            {
                ApplyState(item, state);
                Plugin.Log.LogInfo("[Coop] Applied " + item.name + " " + state.Id + ": " + CountNow(item) + " pieces.");
            }
            finally
            {
                Stall.Report("Applied " + item.name + " from another player", watch);
            }
            return item;
        }

        private static void ApplyState(ShipItem item, BundleState state)
        {

            object sync = ItemSync();
            bool coopWasApplying = sync != null && Get(_remoteApplying, sync);
            int localCount = CountNow(item);
            int countHere = localCount;
            int coopCount;
            if (SetByCoop.TryGetValue(item, out coopCount) && coopCount == localCount)
                localCount = state.BaseCount;
            SetByCoop.Remove(item);
            _applying = true;
            try
            {
                // Keeps Coop from sending on what the rebuild does to the item.
                if (sync != null && !coopWasApplying)
                    _setRemoteApplying.Invoke(sync, new object[] { true });

                bool wasHanging = SausageStacks.IsHanging(item);
                item.amount = state.Amount;
                item.health = state.Health;
                FoodState food = item.GetComponent<FoodState>();
                if (food != null && state.HasFood)
                {
                    food.dried = state.Dried;
                    food.smoked = state.Smoked;
                    food.salted = state.Salted;
                    food.spoiled = state.Spoiled;
                }

                BundleKind kind = FirewoodPieces.KindOf(item);
                if (kind != null && !kind.CountInAmount)
                    WriteStack(item, state.Stack);
                // Someone else changed the count while this was on its way: keep both changes.
                int? merged = CountMerge.Resolve(localCount, state.BaseCount, state.Count);
                // A change made here this frame has not gone out: keep it, and send it as a
                // change from the settled count (J-036).
                int pendingBase;
                if (state.BaseCount == CountMerge.Settled && Changed.Contains(item) && Bases.TryGetValue(item, out pendingBase))
                {
                    merged = CountMerge.KeepPending(countHere, pendingBase, state.Count);
                    Bases[item] = state.Count;
                }
                if (merged.HasValue)
                {
                    if (HookLinePieces.IsHook(item))
                        HookLinePieces.WriteCount(item, merged.Value, HookLinePieces.IsLine(item));
                    else if (kind != null)
                        FirewoodPieces.WriteCount(item, merged.Value);
                    Plugin.Log.LogInfo("[Coop] " + item.name + " " + state.Id + " was changed here too: " + merged.Value + " pieces.");
                }
                Rebuild(item, wasHanging);
            }
            finally
            {
                if (sync != null && !coopWasApplying)
                {
                    try
                    {
                        _setRemoteApplying.Invoke(sync, new object[] { false });
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning("Could not reset Coop's remote state flag: " + ex);
                    }
                }
                _applying = false;
            }
        }

        // The stack as the sender has it, color included, whatever ours was.
        private static void WriteStack(ShipItem item, float saved)
        {
            StackCode code = StackCode.Decode(saved);
            int color = code.Color;
            int count = code.Count;
            int width = code.Width;

            StackState stack = item.GetComponent<StackState>();
            if (code.IsSingle(SausageStacks.HangWidth))
            {
                if (stack != null)
                {
                    stack.Count = 1;
                    stack.Width = 0;
                    stack.Color = 0;
                }
                return;
            }

            if (stack == null)
                stack = item.gameObject.AddComponent<StackState>();
            stack.Count = Mathf.Max(1, count);
            stack.Width = width;
            stack.Color = color;
        }

        private static void Rebuild(ShipItem item, bool wasHanging)
        {
            if (HookLinePieces.IsHook(item))
            {
                if (HookLinePieces.IsLine(item))
                    HookLineBuilder.Apply(item);
                else if (HookLineBuilder.IsBuilt(item))
                    HookLineBuilder.RestoreSingle(item);
                return;
            }

            BundleKind kind = FirewoodPieces.KindOf(item);
            if (kind == null)
                return;
            if (FirewoodPieces.CountOf(item) > 1 || kind.Hangs)
                FirewoodBundleBuilder.Apply(item);
            else if (wasHanging)
                FirewoodBundleBuilder.Unhang(item);
            else if (FirewoodBundleBuilder.IsBuilt(item))
                FirewoodBundleBuilder.RestoreSingle(item);
            item.UpdateLookText();
        }
    }
}
