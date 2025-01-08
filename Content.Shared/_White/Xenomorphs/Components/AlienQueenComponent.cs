﻿using Content.Shared.Actions;
using Content.Shared.Polymorph;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Aliens.Components;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AlienQueenComponent : Component
{
    [DataField]
    public float PlasmaCostEgg = 100f;

    /// <summary>
    /// The egg prototype to use.
    /// </summary>
    [DataField]
    public EntProtoId EggPrototype = "AlienEggGrowing";

    [DataField]
    public EntProtoId? EggAction = "ActionAlienEgg";

    [DataField]
    public EntityUid? EggActionEntity;

    /// <summary>
    /// The entity needed to actually make acid. This will be granted (and removed) upon the entity's creation.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// This will subtract (not add, don't get this mixed up) from the current plasma of the mob making acid.
    /// </summary>
    [DataField]
    public float PlasmaCostRoyalLarva = 300f;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<PolymorphPrototype>))]
    public string PraetorianPolymorphPrototype = "AlienEvolutionPraetorian";
}

public sealed partial class AlienEggActionEvent : InstantActionEvent { }

public sealed partial class RoyalLarvaActionEvent : EntityTargetActionEvent { }
