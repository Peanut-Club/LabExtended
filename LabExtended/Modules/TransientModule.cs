﻿using Common.IO.Collections;
using Common.Pooling.Pools;
using Common.Utilities;

using LabExtended.API;
using LabExtended.Core;
using LabExtended.Events;
using LabExtended.Extensions;

namespace LabExtended.Modules
{
    /// <summary>
    /// A module that is reused once the targeted player re-joins the server.
    /// </summary>
    public class TransientModule : Module
    {
        static TransientModule()
            => UpdateEvent.OnUpdate += UpdateModules;

        /// <summary>
        /// The reason for a module's removal.
        /// </summary>
        public enum RemovalReason : byte
        {
            /// <summary>
            /// The module has requested it by returning <see langword="true"/> in <see cref="TransientModule.OnLeaving"/>.
            /// </summary>
            Requested = 0,

            /// <summary>
            /// The module's lifetime has expired.
            /// </summary>
            Expired = 1,

            /// <summary>
            /// The module's removal is forced by using <see cref="ModuleParent.RemoveModule{T}"/>.
            /// </summary>
            Forced = 2
        }

        internal static readonly LockedDictionary<string, List<TransientModule>> _cachedModules = new LockedDictionary<string, List<TransientModule>>();
        internal static DateTime _tickTimer = DateTime.MinValue;

        internal DateTime? _addedAt;
        internal DateTime? _removedAt;

        internal bool _isCached;
        internal bool _isForced;

        /// <summary>
        /// Gets or sets the delay between ticks for removed modules. Values below one will disable ticking removed modules entirely.
        /// </summary>
        public static int TickDelay { get; set; } = 500;

        /// <summary>
        /// Gets the player that owns this module.
        /// </summary>
        public ExPlayer Player { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the owner is offline or not.
        /// </summary>
        public bool IsOffline => Player is null;

        /// <summary>
        /// Gets the module's ID.
        /// </summary>
        public string ModuleId { get; } = Generator.Instance.GetString(10);

        /// <summary>
        /// Gets the owner's user ID.
        /// </summary>
        public string OwnerId { get; private set; }

        /// <summary>
        /// Gets the time that has passed since the owning player left.
        /// </summary>
        public TimeSpan TimeSinceRemoval => _removedAt.HasValue ? (DateTime.Now - _removedAt.Value) : TimeSpan.Zero;

        /// <summary>
        /// Gets the maximum amount of time that can pass.
        /// </summary>
        public virtual TimeSpan? LifeTime { get; }

        /// <summary>
        /// Whether or not to keep this module active once the player leaves.
        /// </summary>
        public virtual bool KeepActive { get; set; }

        /// <summary>
        /// Gets called when the player joins back / when the module is added for the first time.
        /// </summary>
        public virtual void OnJoined() { }

        /// <summary>
        /// Gets called when the player leaves.
        /// </summary>
        /// <returns><see langword="true"/> if you want to re-add the module once the player returns <i>(default behaviour)</i>, otherwise <see langword="false"/>.</returns>
        public virtual bool OnLeaving() => true;

        /// <summary>
        /// Gets called when the module gets removed from the dictionary.
        /// </summary>
        public virtual void OnRemoved(RemovalReason removalReason) { }

        /// <inheritdoc/>
        public override void Start()
        {
            base.Start();

            if (Parent is not ExPlayer exPlayer)
                throw new InvalidOperationException($"Transient Modules can only be added to the ExPlayer class.");

            Player = exPlayer;

            if (string.IsNullOrWhiteSpace(OwnerId))
                OwnerId = Player.UserId;
            else if (Player.UserId != OwnerId)
                throw new InvalidOperationException($"This module belongs to {OwnerId} and cannot be added to {Player.UserId}");

            if (!_cachedModules.TryGetValue(Player.UserId, out var transientModules))
                _cachedModules[Player.UserId] = transientModules = new List<TransientModule>();

            if (!transientModules.Contains(this))
                transientModules.Add(this);

            _addedAt = DateTime.Now;
            _removedAt = null;
            _isCached = false;

            OnJoined();
        }

        /// <inheritdoc/>
        public override void Stop()
        {
            base.Stop();

            _addedAt = null;
            _removedAt = DateTime.Now;

            if (!_cachedModules.TryGetValue(Player.UserId, out var transientModules))
                _cachedModules[Player.UserId] = transientModules = new List<TransientModule>();

            if (!OnLeaving() || _isForced)
            {
                if (transientModules.Remove(this))
                {
                    ExLoader.Debug("Transient Modules", $"Removed transient module &3{GetType().Name}&r (&6{ModuleId}&r) from player &3{Player.Name}&r (&6{Player.UserId}&r).");
                    OnRemoved(RemovalReason.Requested);
                }
            }
            else
            {
                ExLoader.Debug("Transient Modules", $"Cached transient module &3{GetType().Name}&r (&6{ModuleId}&r) for player &3{Player.Name}&r (&6{Player.UserId}&r).");
                _isCached = true;
            }

            Player = null;
        }

        private static void UpdateModules()
        {
            if (TickDelay < 1)
                return;

            if ((DateTime.Now - _tickTimer).TotalMilliseconds < TickDelay)
                return;

            _tickTimer = DateTime.Now;

            var modulesToRemove = DictionaryPool<string, List<TransientModule>>.Shared.Rent();

            foreach (var modulePair in _cachedModules)
            {
                foreach (var module in modulePair.Value)
                {
                    var type = module.GetType();

                    if (!module.IsActive)
                        continue;

                    if (!module._isCached)
                        continue;

                    if (string.IsNullOrWhiteSpace(module.OwnerId))
                        continue;

                    if (module.LifeTime.HasValue && module.TimeSinceRemoval >= module.LifeTime.Value)
                    {
                        if (!modulesToRemove.TryGetValue(module.OwnerId, out var removedModules))
                            modulesToRemove[module.OwnerId] = removedModules = ListPool<TransientModule>.Shared.Rent();

                        if (!removedModules.Contains(module))
                            removedModules.Add(module);

                        ExLoader.Debug("Transient Modules", $"Removing transient module &3{type.Name}&r (&6{module.ModuleId}&r): life time expired.");

                        module.OnRemoved(RemovalReason.Expired);
                        continue;
                    }

                    try
                    {
                        module.Tick();
                    }
                    catch (Exception ex)
                    {
                        ExLoader.Error("Transient Modules", $"Module &3{type.Name}&r (&6{module.ModuleId}&r) failed to tick!\n{ex.ToColoredString()}");
                    }
                }
            }

            foreach (var removedPair in modulesToRemove)
            {
                if (removedPair.Value is null)
                    continue;

                if (!_cachedModules.TryGetValue(removedPair.Key, out var cachedModules))
                    continue;

                foreach (var removedModule in removedPair.Value)
                    cachedModules.Remove(removedModule);

                ListPool<TransientModule>.Shared.Return(removedPair.Value);
            }

            DictionaryPool<string, List<TransientModule>>.Shared.Return(modulesToRemove);
        }
    }
}