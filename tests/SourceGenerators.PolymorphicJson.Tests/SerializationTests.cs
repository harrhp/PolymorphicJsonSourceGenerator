using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SourceGenerators.PolymorphicJson.Tests;

[TestClass]
public class SerializationTests
{
    [TestMethod, DynamicData(nameof(SerializeData))]
    public void Serialize_success(Abstract obj, string expectedJson)
    {
        var json = JsonSerializer.Serialize(obj);

        Assert.AreEqual(expectedJson, json);
    }

    [TestMethod, DynamicData(nameof(DeserializeData))]
    public void Deserialize_success(string json, object expectedObj)
    {
        var obj = JsonSerializer.Deserialize<Abstract>(json);

        Assert.AreEqual(expectedObj, obj);
    }

    public static IEnumerable<object[]> SerializeData =>
        new[]
        {
            new object[] { new Impl1(), "{\"type\":\"impl1\"}" },
            new object[] { new Impl2("val"), "{\"Value\":\"val\",\"type\":\"impl2\"}" }
        };

    public static IEnumerable<object[]> DeserializeData =>
        new[]
        {
            new object[] { "{\"type\":\"impl1\"}", new Impl1() },
            new object[] { "{\"Value\":\"val\",\"type\":\"impl2\"}", new Impl2("val") },
            new object[] { "{\"type\":\"impl3\"}", new Default() }
        };
}

[JsonPolymorphic("type")]
public abstract partial record Abstract;

[JsonDiscriminator("impl1")]
internal sealed record Impl1 : Abstract;

[JsonDiscriminator("impl2")]
internal sealed record Impl2(string Value) : Abstract;

[JsonDiscriminator(IsDefault = true)]
internal sealed record Default : Abstract;
