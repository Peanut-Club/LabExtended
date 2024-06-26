﻿using Common.Extensions;

using HarmonyLib;

using LabExtended.API;
using LabExtended.Core.Hooking;
using LabExtended.Events.Player;

using PlayerRoles;

using PluginAPI.Events;

using System.Reflection;

namespace LabExtended.Patches.Events
{
    [HarmonyPatch(typeof(PlayerRoleManager), nameof(PlayerRoleManager.ServerSetRole))]
    public static class PlayerSpawningPatch
    {
        private static readonly EventInfo _event = typeof(PlayerRoleManager).Event("OnServerRoleSet");

        public static bool Prefix(PlayerRoleManager __instance, RoleTypeId newRole, RoleChangeReason reason, RoleSpawnFlags spawnFlags)
        {
            var player = ExPlayer.Get(__instance.Hub);

            if (player is null)
                return true;

            EventManager.ExecuteEvent(new PlayerChangeRoleEvent(__instance.Hub, __instance.CurrentRole, newRole, reason));

            var spawningEv = new PlayerSpawningArgs(player, __instance.CurrentRole, newRole, reason, spawnFlags);

            if ((player.Switches.CanChangeRoles && HookRunner.RunCancellable(spawningEv, true))
                || (!__instance._anySet || (newRole is RoleTypeId.None && reason is RoleChangeReason.Destroyed))
                || __instance.isLocalPlayer)
            {
                if (!player.Stats.KeepMaxHealthOnRoleChange)
                    player.Stats._maxHealthOverride.Reset();

                if (!player.FakePosition.KeepOnRoleChange || (!player.FakePosition.KeepOnDeath && newRole is RoleTypeId.Spectator && reason is RoleChangeReason.Died))
                    player.FakePosition.ClearValues();

                if (!player.FakeRole.KeepOnRoleChange || (!player.FakeRole.KeepOnDeath && newRole is RoleTypeId.Spectator && reason is RoleChangeReason.Died))
                    player.FakeRole.ClearValues();

                newRole = spawningEv.NewRole;
                reason = spawningEv.ChangeReason;
                spawnFlags = spawningEv.SpawnFlags;

                try
                {
                    _event.Raise(null, __instance.Hub, newRole, reason);
                }
                catch { }

                __instance.InitializeNewRole(newRole, reason, spawnFlags);
            }

            return false;
        }
    }
}
