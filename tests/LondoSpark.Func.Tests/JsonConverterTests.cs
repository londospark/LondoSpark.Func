using System;
using System.Text.Json;
using System.Threading.Tasks;

using LondoSpark.Func.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static LondoSpark.Func.Option<int>;
using static LondoSpark.Func.Result<int, string>;

namespace LondoSpark.Func.Tests;

// Several tests exercise distinct serializer configurations, so each builds its own options.
#pragma warning disable CA1869

public sealed class JsonConverterTests
{
    private static readonly JsonSerializerOptions Options =
        new JsonSerializerOptions().AddLondoSparkFuncConverters();

    [Test]
    public async Task OptionSerializesNoneAsNull()
    {
        var json = JsonSerializer.Serialize(Option.Empty<int>(), Options);

        await Assert.That(json).IsEqualTo("null");
        await Assert.That(JsonSerializer.Deserialize<Option<int>>(json, Options) is None).IsTrue();
    }

    [Test]
    public async Task OptionSerializesSomeAsObject()
    {
        var json = JsonSerializer.Serialize(Option.Return(42), Options);

        await Assert.That(json).IsEqualTo("""{"some":42}""");
        await Assert.That(JsonSerializer.Deserialize<Option<int>>(json, Options) is Some(42)).IsTrue();
    }

    [Test]
    public async Task OptionPreservesSomeNull()
    {
        var json = JsonSerializer.Serialize(Option.Return<string?>(null), Options);

        await Assert.That(json).IsEqualTo("""{"some":null}""");
        await Assert.That(
            JsonSerializer.Deserialize<Option<string?>>(json, Options) is Option<string?>.Some(null)).IsTrue();
    }

    [Test]
    public async Task ResultRoundTripsBothCases()
    {
        var successJson = JsonSerializer.Serialize(Result.Return<int, string>(7), Options);
        var failureJson = JsonSerializer.Serialize(Result.Failure<int, string>("invalid"), Options);

        await Assert.That(successJson).IsEqualTo("""{"success":7}""");
        await Assert.That(failureJson).IsEqualTo("""{"failure":"invalid"}""");
        await Assert.That(
            JsonSerializer.Deserialize<Result<int, string>>(successJson, Options) is Success(7)).IsTrue();
        await Assert.That(
            JsonSerializer.Deserialize<Result<int, string>>(failureJson, Options) is Failure("invalid")).IsTrue();
    }

    [Test]
    public async Task OptionRejectsPayloadsThatAreNotAnOption()
    {
        var exception = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Option<int>>("42", Options));

        await Assert.That(exception.Message).Contains("none");
    }

    [Test]
    public async Task ResultRejectsPayloadsThatAreNotAResult()
    {
        var exception = Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Result<int, string>>("""{"unexpected":1}""", Options));

        await Assert.That(exception.Message).Contains("success");
    }

    [Test]
    public async Task CasePropertyNamesFollowTheNamingPolicy()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.KebabCaseUpper }
            .AddLondoSparkFuncConverters();

        var option = JsonSerializer.Serialize(Option.Return(1), options);
        var result = JsonSerializer.Serialize(Result.Failure<int, string>("x"), options);

        await Assert.That(option).IsEqualTo("""{"SOME":1}""");
        await Assert.That(result).IsEqualTo("""{"FAILURE":"x"}""");
        await Assert.That(JsonSerializer.Deserialize<Option<int>>(option, options) is Some(1)).IsTrue();
        await Assert.That(JsonSerializer.Deserialize<Result<int, string>>(result, options) is Failure("x")).IsTrue();
    }

    [Test]
    public async Task CasePropertyNamesHonourCaseInsensitiveReading()
    {
        var insensitive = new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            .AddLondoSparkFuncConverters();

        await Assert.That(JsonSerializer.Deserialize<Option<int>>("""{"Some":1}""", insensitive) is Some(1)).IsTrue();
        await Assert.That(JsonSerializer.Deserialize<Result<int, string>>("""{"SUCCESS":2}""", insensitive) is Success(2)).IsTrue();
        await Assert.That(() => JsonSerializer.Deserialize<Option<int>>("""{"Some":1}""", Options)).Throws<JsonException>();
    }

    [Test]
    public async Task ClosedConvertersWorkWithoutTheReflectionFactory()
    {
        var options = new JsonSerializerOptions()
            .AddOptionConverter<int>()
            .AddResultConverter<int, string>();

        await Assert.That(JsonSerializer.Serialize(Option.Return(3), options)).IsEqualTo("""{"some":3}""");
        await Assert.That(JsonSerializer.Serialize(Result.Return<int, string>(4), options)).IsEqualTo("""{"success":4}""");
    }

    [Test]
    public async Task OneConverterInstanceServesSeveralConfigurations()
    {
        var shared = new OptionJsonConverter<int>();
        var plain = new JsonSerializerOptions { Converters = { shared } };
        var upper = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper, Converters = { shared } };

        await Assert.That(JsonSerializer.Serialize(Option.Return(1), plain)).IsEqualTo("""{"some":1}""");
        await Assert.That(JsonSerializer.Serialize(Option.Return(1), upper)).IsEqualTo("""{"SOME":1}""");
        await Assert.That(JsonSerializer.Serialize(Option.Return(1), plain)).IsEqualTo("""{"some":1}""");
    }

    [Test]
    public async Task RecursivePayloadsRoundTrip()
    {
        var chain = new Node(1, Option.Return(new Node(2, Option.Empty<Node>())));

        var json = JsonSerializer.Serialize(chain, Options);
        var read = JsonSerializer.Deserialize<Node>(json, Options)!;

        await Assert.That(json).IsEqualTo("""{"Value":1,"Next":{"some":{"Value":2,"Next":null}}}""");
        await Assert.That(read.Next is Option<Node>.Some { Value: { Value: 2, Next: None } }).IsTrue();
    }

    public sealed record Node(int Value, Option<Node> Next);
}
