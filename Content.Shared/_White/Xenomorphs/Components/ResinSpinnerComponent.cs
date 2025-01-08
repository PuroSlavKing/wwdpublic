﻿using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Aliens.Components;


/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ResinSpinnerComponent : Component
{
    [DataField]
    public string PopupText = "alien-action-fail-plasma";

    /// <summary>
    /// How long will it take to make.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float ProductionLengthWall = 0.5f;

    /// <summary>
    /// This will subtract (not add, don't get this mixed up) from the current plasma of the mob making structure.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float PlasmaCostWall = 50f;

    /// <summary>
    /// The wall prototype to use.
    /// </summary>
    [DataField]
    public EntProtoId WallPrototype = "WallResin";

    [DataField]
    public EntProtoId? ResinWallAction = "ActionAlienDroneWall";

    [DataField]
    public EntityUid? ResinWallActionEntity;

    /// <summary>
    /// How long will it take to make.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float ProductionLengthWindow = 0.5f;

    /// <summary>
    /// This will subtract (not add, don't get this mixed up) from the current plasma of the mob making structure.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float PlasmaCostWindow = 50f;

    /// <summary>
    /// The wall prototype to use.
    /// </summary>
    [DataField]
    public EntProtoId WindowPrototype = "WindowResin";

    [DataField]
    public EntProtoId? ResinWindowAction = "ActionWindowResin";

    [DataField]
    public EntityUid? ResinWindowActionEntity;

    /// <summary>
    /// How long will it take to make.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float ProductionLengthNest = 0.5f;

    /// <summary>
    /// This will subtract (not add, don't get this mixed up) from the current plasma of the mob making structure.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float PlasmaCostNest = 50f;

    /// <summary>
    /// The wall prototype to use.
    /// </summary>
    [DataField]
    public EntProtoId NestPrototype = "AlienNest";

    [DataField]
    public EntProtoId? NestAction = "ActionAlienNest";

    [DataField]
    public EntityUid? NestActionEntity;
}
