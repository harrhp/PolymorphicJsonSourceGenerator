namespace SourceGenerators.PolymorphicJson.Samples;

[JsonPolymorphic("type")]
public abstract partial record AbstractRecord2;

[JsonDiscriminator("impl1")]
public sealed record AbstractRecord2Impl1 : AbstractRecord2;

[JsonDiscriminator(IsDefault = true)]
public sealed record AbstractRecord2Default : AbstractRecord2;
