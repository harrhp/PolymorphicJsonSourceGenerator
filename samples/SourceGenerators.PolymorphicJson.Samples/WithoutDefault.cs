namespace SourceGenerators.PolymorphicJson.Samples;

[JsonPolymorphic("type")]
public abstract partial record AbstractRecord1;

[JsonDiscriminator("impl1")]
public sealed record AbstractRecord1Impl1 : AbstractRecord1;

[JsonDiscriminator("impl2")]
public sealed record AbstractRecord1Impl2 : AbstractRecord1;
