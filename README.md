# PolymorphicJsonSourceGenerator for System.Text.Json

## Why?

System.Text.Json doesn't support polymorphic deserialization and serialization.

## How it works

This source generator generates `JsonConverter` for base type of hierarchy that determines concrete type of object and delegates further work to `JsonSerializer`

## Limitations

Works only with records.

## How to use

Annotate your base type with `JsonPolymorphicAttribute` and your derived types with `JsonDiscriminatorAttribute`

```c#
[JsonPolymorphic("type")]
public abstract partial record AbstractRecord1;

[JsonDiscriminator("impl1")]
public sealed record AbstractRecord1Impl1 : AbstractRecord1;

[JsonDiscriminator("impl2")]
public sealed record AbstractRecord1Impl2 : AbstractRecord1;
```

If during deserialization unknown discriminator is encountered exception will be thrown. To avoid this add derived type marked as default like this

```c#
[JsonPolymorphic("type")]
public abstract partial record AbstractRecord2;

[JsonDiscriminator("impl1")]
public sealed record AbstractRecord2Impl1 : AbstractRecord2;

[JsonDiscriminator(IsDefault = true)]
public sealed record AbstractRecord2Default : AbstractRecord2;
```

now every payload with unknown discriminator will be deserialized to default type
