using Robust.Shared.Serialization;
namespace Content.Shared._Horizon.Planets.Procedural;

[DataDefinition, NetSerializable, Serializable]
public sealed partial class ProceduralPlanetData
{
    [DataField("shader"), ViewVariables(VVAccess.ReadWrite)]
    public string ShaderName = "ProceduralGalaxy";

    [ViewVariables(VVAccess.ReadWrite), DataField("zoom")]
    public float Zoom = 1.375f;

    [ViewVariables(VVAccess.ReadWrite), DataField("pixels")]
    public float Pixels = 100f;
    [ViewVariables(VVAccess.ReadWrite), DataField("rotation")]
    public float Rotation = 0.674f;
    [ViewVariables(VVAccess.ReadWrite), DataField("timeSpeed")]
    public float TimeSpeed = 1.0f;
    [ViewVariables(VVAccess.ReadWrite), DataField("ditherSize")]
    public float DitherSize = 2.0f;
    [ViewVariables(VVAccess.ReadWrite), DataField("sitherSize")]
    public bool ShouldDither = true;
    [ViewVariables(VVAccess.ReadWrite), DataField("nColors")]
    public int NColors = 6;

    [ViewVariables(VVAccess.ReadWrite), DataField("size")]
    public float Size = 4.0f;
    [ViewVariables(VVAccess.ReadWrite), DataField("seed")]
    public float Seed = 5.881f;

    public float Time = 0.0f;

    [ViewVariables(VVAccess.ReadWrite), DataField("tilt")]
    public float Tilt = 3.0f;
    [ViewVariables(VVAccess.ReadWrite), DataField("nLayers")]
    public float NLayers = 4.0f;
    [ViewVariables(VVAccess.ReadWrite), DataField("layerHeight")]
    public float LayerHeight = 0.4f;
    [ViewVariables(VVAccess.ReadWrite), DataField("swirl")]
    public float Swirl = -9.0f;

    [DataField("color0"), ViewVariables(VVAccess.ReadWrite)]
    public Color Color0 = Color.FromHex("#FFFFEB");
    [DataField("color1"), ViewVariables(VVAccess.ReadWrite)]
    public Color Color1 = Color.FromHex("#FFE98D");
    [DataField("color2"), ViewVariables(VVAccess.ReadWrite)]
    public Color Color2 = Color.FromHex("#B5E066");
    [DataField("color3"), ViewVariables(VVAccess.ReadWrite)]
    public Color Color3 = Color.FromHex("#65A566");
    [DataField("color4"), ViewVariables(VVAccess.ReadWrite)]
    public Color Color4 = Color.FromHex("#395D64");
    [DataField("color5"), ViewVariables(VVAccess.ReadWrite)]
    public Color Color5 = Color.FromHex("#32394D");
    [DataField("color6"), ViewVariables(VVAccess.ReadWrite)]
    public Color Color6 = Color.FromHex("#322947");
}
