﻿using HarmonyLib;

using PlayerRoles;

namespace LabExtended.Patches.Functions.SpectatorList
{
    [HarmonyPatch(typeof(PlayerRoleManager), nameof(PlayerRoleManager.Update))]
    public static class SpectatorListSyncRolePatch
    {
        public static bool Prefix(PlayerRoleManager __instance) => __instance._hub is null || __instance.isLocalPlayer;
    }
}