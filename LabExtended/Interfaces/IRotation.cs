﻿using UnityEngine;

namespace LabExtended.Interfaces
{
    /// <summary>
    /// Represents an object that has a rotation.
    /// </summary>
    public interface IRotation
    {
        /// <summary>
        /// Gets the object's current rotation as a <see cref="Quaternion"/>.
        /// </summary>
        Quaternion Rotation { get; }

        /// <summary>
        /// Gets the object's current rotation as a <see cref="Vector2"/>.
        /// </summary>
        Vector2 RotationV2 { get; }

        /// <summary>
        /// Gets the object's current rotation as a <see cref="Vector3"/>.
        /// </summary>
        Vector3 RotationV3 { get; }
    }
}
