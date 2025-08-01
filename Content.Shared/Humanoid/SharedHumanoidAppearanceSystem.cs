using System.IO;
using System.Linq;
using System.Numerics;
using Content.Shared._EE.Contractors.Prototypes;
using Content.Shared._White.Humanoid.Prototypes;
using Content.Shared._White.TTS;
using Content.Shared.Examine;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared._Shitmed.Humanoid.Events; // Shitmed Change
using Content.Shared.IdentityManagement;
using Content.Shared.Preferences;
using Content.Shared.HeightAdjust;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Shadowkin;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;
using Content.Shared._EE.GenderChange;

namespace Content.Shared.Humanoid;

/// <summary>
///     HumanoidSystem. Primarily deals with the appearance and visual data
///     of a humanoid entity. HumanoidVisualizer is what deals with actually
///     organizing the sprites and setting up the sprite component's layers.
///
///     This is a shared system, because while it is server authoritative,
///     you still need a local copy so that players can set up their
///     characters.
/// </summary>
public abstract class SharedHumanoidAppearanceSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfgManager = default!;
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly ISerializationManager _serManager = default!;
    [Dependency] private readonly HeightAdjustSystem _heightAdjust = default!;
    [Dependency] private readonly ISharedPlayerManager _sharedPlayerManager = default!;

    [ValidatePrototypeId<SpeciesPrototype>]
    public const string DefaultSpecies = "Human";

    [ValidatePrototypeId<EmployerPrototype>]
    public const string DefaultEmployer = "NanoTrasen";

    [ValidatePrototypeId<NationalityPrototype>]
    public const string DefaultNationality = "Bieselite";

    [ValidatePrototypeId<LifepathPrototype>]
    public const string DefaultLifepath = "Spacer";

    // WD EDIT START
    [ValidatePrototypeId<BodyTypePrototype>]
    public const string DefaultBodyType = "HumanNormal";

    public const string DefaultVoice = "Aidar";

    public static readonly Dictionary<Sex, string> DefaultSexVoice = new()
    {
        { Sex.Male, "Aidar" },
        { Sex.Female, "Kseniya" },
        { Sex.Unsexed, "Baya" },
    };
    // WD EDIT END

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HumanoidAppearanceComponent, ExaminedEvent>(OnExamined);
    }

    public DataNode ToDataNode(HumanoidCharacterProfile profile)
    {
        var export = new HumanoidProfileExport
        {
            ForkId = _cfgManager.GetCVar(CVars.BuildForkId),
            Profile = profile,
        };

        var dataNode = _serManager.WriteValue(export, alwaysWrite: true, notNullableOverride: true);
        return dataNode;
    }

    public HumanoidCharacterProfile FromStream(Stream stream, ICommonSession session)
    {
        using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
        var yamlStream = new YamlStream();
        yamlStream.Load(reader);

        var root = yamlStream.Documents[0].RootNode;
        var export = _serManager.Read<HumanoidProfileExport>(root.ToDataNode(), notNullableOverride: true);

        // Add custom handling here for forks / version numbers if you care

        var profile = export.Profile;
        var collection = IoCManager.Instance;
        profile.EnsureValid(session, collection!);
        return profile;
    }

    private void OnInit(EntityUid uid, HumanoidAppearanceComponent humanoid, ComponentInit args)
    {
        if (string.IsNullOrEmpty(humanoid.Species) || _netManager.IsClient && !IsClientSide(uid))
            return;

        if (string.IsNullOrEmpty(humanoid.Initial)
            || !_proto.TryIndex(humanoid.Initial, out HumanoidProfilePrototype? startingSet))
        {
            LoadProfile(uid, HumanoidCharacterProfile.DefaultWithSpecies(humanoid.Species), humanoid, false, false);
            return;
        }

        // Do this first, because profiles currently do not support custom base layers
        foreach (var (layer, info) in startingSet.CustomBaseLayers)
            humanoid.CustomBaseLayers.Add(layer, info);

        LoadProfile(uid, startingSet.Profile, humanoid, false, false);
    }

    private void OnExamined(EntityUid uid, HumanoidAppearanceComponent component, ExaminedEvent args)
    {
        var identity = Identity.Entity(uid, EntityManager);
        var species = GetSpeciesRepresentation(component.Species, component.CustomSpecieName).ToLower();
        var age = GetAgeRepresentation(component.Species, component.Age);
        if (HasComp<ShadowkinComponent>(uid))
        {
            var color = component.EyeColor.Name();
            if (color != null)
                age = Loc.GetString("identity-eye-shadowkin", ("color", color));
        }

        // WWDP EDIT
        string locale = "humanoid-appearance-component-examine";

        if (args.Examiner == args.Examined) // Use the selfaware locale when examining yourself
            locale += "-selfaware";

        args.PushText(Loc.GetString(locale, ("user", identity), ("age", age), ("species", species)), 100); // priority for examine
        // WWDP EDIT END
        if (component.DisplayPronouns != null)
            args.PushText(Loc.GetString("humanoid-appearance-component-examine-pronouns", ("user", identity), ("pronouns", component.DisplayPronouns)));
    }

    /// <summary>
    ///     Toggles a humanoid's sprite layer visibility.
    /// </summary>
    /// <param name="uid">Humanoid mob's UID</param>
    /// <param name="layer">Layer to toggle visibility for</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetLayerVisibility(EntityUid uid,
        HumanoidVisualLayers layer,
        bool visible,
        bool permanent = false,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid, false))
            return;

        var dirty = false;
        SetLayerVisibility(uid, humanoid, layer, visible, permanent, ref dirty);
        if (dirty)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Sets the visibility for multiple layers at once on a humanoid's sprite.
    /// </summary>
    /// <param name="uid">Humanoid mob's UID</param>
    /// <param name="layers">An enumerable of all sprite layers that are going to have their visibility set</param>
    /// <param name="visible">The visibility state of the layers given</param>
    /// <param name="permanent">If this is a permanent change, or temporary. Permanent layers are stored in their own hash set.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetLayersVisibility(EntityUid uid, IEnumerable<HumanoidVisualLayers> layers, bool visible, bool permanent = false,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        var dirty = false;

        foreach (var layer in layers)
            SetLayerVisibility(uid, humanoid, layer, visible, permanent, ref dirty);

        if (dirty)
            Dirty(uid, humanoid);
    }

    protected virtual void SetLayerVisibility(
        EntityUid uid,
        HumanoidAppearanceComponent humanoid,
        HumanoidVisualLayers layer,
        bool visible,
        bool permanent,
        ref bool dirty)
    {
        if (visible)
        {
            if (permanent)
                dirty |= humanoid.PermanentlyHidden.Remove(layer);

            dirty |= humanoid.HiddenLayers.Remove(layer);
        }
        else
        {
            if (permanent)
                dirty |= humanoid.PermanentlyHidden.Add(layer);

            dirty |= humanoid.HiddenLayers.Add(layer);
        }
    }

    /// <summary>
    ///     Set a humanoid mob's species. This will change their base sprites, as well as their current
    ///     set of markings to fit against the mob's new species.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="species">The species to set the mob to. Will return if the species prototype was invalid.</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetSpecies(EntityUid uid, string species, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || !_proto.TryIndex<SpeciesPrototype>(species, out var prototype))
            return;

        humanoid.Species = species;
        humanoid.MarkingSet.EnsureSpecies(species, humanoid.SkinColor, _markingManager);
        var oldMarkings = humanoid.MarkingSet.GetForwardEnumerator().ToList();
        humanoid.MarkingSet = new(oldMarkings, prototype.MarkingPoints, _markingManager, _proto);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Sets the skin color of this humanoid mob. Will only affect base layers that are not custom,
    ///     custom base layers should use <see cref="SetBaseLayerColor"/> instead.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="skinColor">Skin color to set on the humanoid mob.</param>
    /// <param name="sync">Whether to synchronize this to the humanoid mob, or not.</param>
    /// <param name="verify">Whether to verify the skin color can be set on this humanoid or not</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public virtual void SetSkinColor(EntityUid uid, Color skinColor, bool sync = true, bool verify = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        if (!_proto.TryIndex<SpeciesPrototype>(humanoid.Species, out var species))
            return;

        if (verify && !SkinColor.VerifySkinColor(species.SkinColoration, skinColor))
            skinColor = SkinColor.ValidSkinTone(species.SkinColoration, skinColor);

        humanoid.SkinColor = skinColor;

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Sets the base layer ID of this humanoid mob. A humanoid mob's 'base layer' is
    ///     the skin sprite that is applied to the mob's sprite upon appearance refresh.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="layer">The layer to target on this humanoid mob.</param>
    /// <param name="id">The ID of the sprite to use. See <see cref="HumanoidSpeciesSpriteLayer"/>.</param>
    /// <param name="sync">Whether to synchronize this to the humanoid mob, or not.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetBaseLayerId(EntityUid uid, HumanoidVisualLayers layer, string? id, bool sync = true,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        if (humanoid.CustomBaseLayers.TryGetValue(layer, out var info))
            humanoid.CustomBaseLayers[layer] = info with { Id = id };
        else
            humanoid.CustomBaseLayers[layer] = new(id);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Sets the color of this humanoid mob's base layer. See <see cref="SetBaseLayerId"/> for a
    ///     description of how base layers work.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="layer">The layer to target on this humanoid mob.</param>
    /// <param name="color">The color to set this base layer to.</param>
    public void SetBaseLayerColor(EntityUid uid, HumanoidVisualLayers layer, Color? color, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        if (humanoid.CustomBaseLayers.TryGetValue(layer, out var info))
            humanoid.CustomBaseLayers[layer] = info with { Color = color };
        else
            humanoid.CustomBaseLayers[layer] = new(null, color);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Set a humanoid mob's sex. This will not change their gender.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="sex">The sex to set the mob to.</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetSex(EntityUid uid, Sex sex, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || humanoid.Sex == sex)
            return;

        var oldSex = humanoid.Sex;
        humanoid.Sex = sex;
        humanoid.MarkingSet.EnsureSexes(sex, _markingManager);
        RaiseLocalEvent(uid, new SexChangedEvent(oldSex, sex));

        if (sync)
            Dirty(uid, humanoid);
    }

    // WD EDIT START
    /// <summary>
    ///     Set a humanoid mob's voice type.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="voiceId">The tts voice to set the mob to.</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    // ReSharper disable once InconsistentNaming
    public void SetTTSVoice(
        EntityUid uid,
        ProtoId<TTSVoicePrototype> voiceId,
        bool sync = true,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!TryComp<TTSComponent>(uid, out var comp)
            || !Resolve(uid, ref humanoid))
            return;

        humanoid.Voice = voiceId;
        comp.VoicePrototypeId = voiceId;

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Set a humanoid mob's body tupe. This will change their base sprites.
    /// </summary>
    /// <param name="uid">The humanoid mob's UID.</param>
    /// <param name="bodyType">The body type to set the mob to. Will return if the body type prototype was invalid.</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetBodyType(
        EntityUid uid,
        ProtoId<BodyTypePrototype> bodyType,
        bool sync = true,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        var speciesPrototype = _proto.Index<SpeciesPrototype>(humanoid.Species);
        if (speciesPrototype.BodyTypes.Contains(bodyType))
            humanoid.BodyType = bodyType;
        else
            humanoid.BodyType = speciesPrototype.BodyTypes.First();

        if (sync)
            Dirty(uid, humanoid);
    }
    // WD EDIT END

    /// <summary>
    ///     Set the height of a humanoid mob
    /// </summary>
    /// <param name="uid">The humanoid mob's UID</param>
    /// <param name="height">The height to set the mob to</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetHeight(EntityUid uid, float height, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || MathHelper.CloseTo(humanoid.Height, height, 0.001f))
            return;

        var species = _proto.Index(humanoid.Species);
        humanoid.Height = Math.Clamp(height, species.MinHeight, species.MaxHeight);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Set the width of a humanoid mob
    /// </summary>
    /// <param name="uid">The humanoid mob's UID</param>
    /// <param name="width">The width to set the mob to</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetWidth(EntityUid uid, float width, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || MathHelper.CloseTo(humanoid.Width, width, 0.001f))
            return;

        var species = _proto.Index(humanoid.Species);
        humanoid.Width = Math.Clamp(width, species.MinWidth, species.MaxWidth);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Set the scale of a humanoid mob
    /// </summary>
    /// <param name="uid">The humanoid mob's UID</param>
    /// <param name="scale">The scale to set the mob to</param>
    /// <param name="sync">Whether to immediately synchronize this to the humanoid mob, or not</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void SetScale(EntityUid uid, Vector2 scale, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        var species = _proto.Index(humanoid.Species);
        humanoid.Height = Math.Clamp(scale.Y, species.MinHeight, species.MaxHeight);
        humanoid.Width = Math.Clamp(scale.X, species.MinWidth, species.MaxWidth);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    ///     Loads a humanoid character profile directly onto this humanoid mob.
    /// </summary>
    /// <param name="uid">The mob's entity UID.</param>
    /// <param name="profile">The character profile to load.</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public virtual void LoadProfile(EntityUid uid,
        HumanoidCharacterProfile? profile,
        HumanoidAppearanceComponent? humanoid = null,
        bool loadExtensions = true,
        bool generateLoadouts = true)
    {
        if (profile == null)
            return;

        if (!Resolve(uid, ref humanoid))
            return;

        SetSpecies(uid, profile.Species, false, humanoid);
        SetSex(uid, profile.Sex, false, humanoid);
        SetTTSVoice(uid, profile.Voice, false, humanoid); // WD EDIT
        SetBodyType(uid, profile.BodyType, false, humanoid); // WD EDIT

        humanoid.Gender = profile.Gender;
        if (TryComp<GrammarComponent>(uid, out var grammar))
            grammar.Gender = profile.Gender;

        humanoid.DisplayPronouns = profile.DisplayPronouns;
        humanoid.StationAiName = profile.StationAiName;
        humanoid.CyborgName = profile.CyborgName;
        humanoid.Age = profile.Age;

        humanoid.CustomSpecieName = profile.Customspeciename;

        humanoid.EyeColor = profile.Appearance.EyeColor;
        var ev = new EyeColorInitEvent();
        RaiseLocalEvent(uid, ref ev);

        humanoid.LastProfileLoaded = profile; //Set the loaded profile because Traits are about to need it
        SetSkinColor(uid, profile.Appearance.SkinColor, false);
        if (loadExtensions && _sharedPlayerManager.TryGetSessionByEntity(uid, out var session))
            RaiseLocalEvent(uid, new LoadProfileExtensionsEvent(uid, session, null, profile, generateLoadouts));

        SetMarkings(uid, profile, humanoid);

        var species = _proto.Index(humanoid.Species);

        if (profile.Height <= 0 || profile.Width <= 0)
            SetScale(uid, new Vector2(species.DefaultWidth, species.DefaultHeight), true, humanoid);
        else
            SetScale(uid, new Vector2(profile.Width, profile.Height), true, humanoid);

        _heightAdjust.SetScale(uid, new Vector2(humanoid.Width, humanoid.Height));

        humanoid.LastProfileLoaded = profile; //But traits can also modify the profile so we need to set it again.
        Dirty(uid, humanoid);
        RaiseLocalEvent(uid, new ProfileLoadFinishedEvent());
    }

    public void SetMarkings(EntityUid uid, HumanoidCharacterProfile profile, HumanoidAppearanceComponent humanoid)
    {
        humanoid.MarkingSet.Clear();
        // Add markings that doesn't need coloring. We store them until we add all other markings that doesn't need it.
        var markingFColored = new Dictionary<Marking, MarkingPrototype>();
        foreach (var marking in profile.Appearance.Markings)
        {
            if (!_markingManager.TryGetMarking(marking, out var prototype))
                continue;

            if (!prototype.ForcedColoring)
                AddMarking(uid, marking.MarkingId, marking.MarkingColors, false);
            else
                markingFColored.Add(marking, prototype);
        }

        // Hair/facial hair - this may eventually be deprecated.
        // We need to ensure hair before applying it or coloring can try depend on markings that can be invalid
        var hairColor = _markingManager.MustMatchSkin(profile.Species, HumanoidVisualLayers.Hair, out var hairAlpha, _proto)
            ? profile.Appearance.SkinColor.WithAlpha(hairAlpha) : profile.Appearance.HairColor;
        var facialHairColor = _markingManager.MustMatchSkin(profile.Species, HumanoidVisualLayers.FacialHair, out var facialHairAlpha, _proto)
            ? profile.Appearance.SkinColor.WithAlpha(facialHairAlpha) : profile.Appearance.FacialHairColor;

        if (_markingManager.Markings.TryGetValue(profile.Appearance.HairStyleId, out var hairPrototype) &&
            _markingManager.CanBeApplied(profile.Species, profile.Sex, hairPrototype, _proto))
            AddMarking(uid, profile.Appearance.HairStyleId, hairColor, false);

        if (_markingManager.Markings.TryGetValue(profile.Appearance.FacialHairStyleId, out var facialHairPrototype) &&
            _markingManager.CanBeApplied(profile.Species, profile.Sex, facialHairPrototype, _proto))
            AddMarking(uid, profile.Appearance.FacialHairStyleId, facialHairColor, false);

        humanoid.MarkingSet.EnsureSpecies(profile.Species, profile.Appearance.SkinColor, _markingManager, _proto);

        // Finally adding marking with forced colors
        foreach (var (marking, prototype) in markingFColored)
        {
            var markingColors = MarkingColoring.GetMarkingLayerColors(
                prototype,
                profile.Appearance.SkinColor,
                profile.Appearance.EyeColor,
                humanoid.MarkingSet
            );
            AddMarking(uid, marking.MarkingId, markingColors, false);
        }

        EnsureDefaultMarkings(uid, humanoid);
    }

    /// <summary>
    ///     Adds a marking to this humanoid.
    /// </summary>
    /// <param name="uid">Humanoid mob's UID</param>
    /// <param name="marking">Marking ID to use</param>
    /// <param name="color">Color to apply to all marking layers of this marking</param>
    /// <param name="sync">Whether to immediately sync this marking or not</param>
    /// <param name="forced">If this marking was forced (ignores marking points)</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void AddMarking(EntityUid uid, string marking, Color? color = null, bool sync = true, bool forced = false, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid)
            || !_markingManager.Markings.TryGetValue(marking, out var prototype))
            return;

        var markingObject = prototype.AsMarking();
        markingObject.Forced = forced;
        if (color != null)
        {
            for (var i = 0; i < prototype.Sprites.Count; i++)
            {
                markingObject.SetColor(i, color.Value);
            }
        }

        humanoid.MarkingSet.AddBack(prototype.MarkingCategory, markingObject);

        if (sync)
            Dirty(uid, humanoid);
    }

    private void EnsureDefaultMarkings(EntityUid uid, HumanoidAppearanceComponent? humanoid)
    {
        if (!Resolve(uid, ref humanoid))
            return;
        humanoid.MarkingSet.EnsureDefault(humanoid.SkinColor, humanoid.EyeColor, _markingManager);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="uid">Humanoid mob's UID</param>
    /// <param name="marking">Marking ID to use</param>
    /// <param name="colors">Colors to apply against this marking's set of sprites.</param>
    /// <param name="sync">Whether to immediately sync this marking or not</param>
    /// <param name="forced">If this marking was forced (ignores marking points)</param>
    /// <param name="humanoid">Humanoid component of the entity</param>
    public void AddMarking(EntityUid uid, string marking, IReadOnlyList<Color> colors, bool sync = true, bool forced = false, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid)
            || !_markingManager.Markings.TryGetValue(marking, out var prototype))
            return;

        var markingObject = new Marking(marking, colors);
        markingObject.Forced = forced;
        humanoid.MarkingSet.AddBack(prototype.MarkingCategory, markingObject);

        if (sync)
            Dirty(uid, humanoid);
    }

    /// <summary>
    /// Takes ID of the species prototype, returns UI-friendly name of the species.
    /// </summary>
    public string GetSpeciesRepresentation(string speciesId, string? customespeciename)
    {
        if (!string.IsNullOrEmpty(customespeciename))
            return Loc.GetString(customespeciename);

        if (_proto.TryIndex<SpeciesPrototype>(speciesId, out var species))
            return Loc.GetString(species.Name);

        Log.Error("Tried to get representation of unknown species: {speciesId}");
        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    public string GetAgeRepresentation(string species, int age)
    {
        if (!_proto.TryIndex<SpeciesPrototype>(species, out var speciesPrototype))
        {
            Log.Error("Tried to get age representation of species that couldn't be indexed: " + species);
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.YoungAge)
            return Loc.GetString("identity-age-young");

        if (age < speciesPrototype.OldAge)
            return Loc.GetString("identity-age-middle-aged");

        return Loc.GetString("identity-age-old");
    }
    /// <summary>
    ///     Set a humanoid mob's gender.
    /// </summary>
    public void SetGender(EntityUid uid, Robust.Shared.Enums.Gender gender, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || humanoid.Gender == gender)
            return;
        if (TryComp<GrammarComponent>(uid, out var grammar))
        {
            grammar.Gender = gender;
            Dirty(uid, grammar);
        }
        humanoid.Gender = gender;
        RaiseLocalEvent(uid, new GenderChangeEvent(uid, gender), true);
        Dirty(uid, humanoid);
    }
}
