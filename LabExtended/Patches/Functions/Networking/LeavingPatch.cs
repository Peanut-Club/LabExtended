﻿using HarmonyLib;

using LabExtended.API;
using LabExtended.Core.Hooking;
using LabExtended.Events.Player;

using LiteNetLib;

using Mirror.LiteNetLib4Mirror;

namespace LabExtended.Patches.Functions.Networking
{
    [HarmonyPatch(typeof(LiteNetLib4MirrorServer), nameof(LiteNetLib4MirrorServer.OnPeerDisconnected))]
    public static class LeavingPatch
    {
        public static bool Prefix(NetPeer peer, DisconnectInfo disconnectinfo)
        {
            if (peer is null)
                return true;

            var player = ExPlayer.Get(peer);

            if (player is null)
                return true;

            HookManager.ExecuteCustom(new PlayerLeavingArgs(player, disconnectinfo.Reason is DisconnectReason.Timeout, disconnectinfo));
            return true;
        }
    }
}