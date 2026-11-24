using System;
using System.Text.Json;
using System.Threading.Tasks;

using LondoSpark.Func;
using LondoSpark.Func.Json;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

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
        await Assert.That(JsonSerializer.Deserialize<Option<int>>(json, Options) is Some<int> { Value: 42 }).IsTrue();
    }

    [Test]
    public async Task OptionPreservesSomeNull()
    {
        var json = JsonSerializer.Serialize(Option.Return<string?>(null), Options);

        await Assert.That(json).IsEqualTo("""{"some":null}""");
        await Assert.That(
            JsonSerializer.Deserialize<Option<string?>>(json, Options) is Some<string?> { Value: null }).IsTrue();
    }

    [Test]
    public async Task ResultRoundTripsBothCases()
    {
        var successJson = JsonSerializer.Serialize(Result.Return<int, string>(7), Options);
        var failureJson = JsonSerializer.Serialize(Result.Failure<int, string>("invalid"), Options);

        await Assert.That(successJson).IsEqualTo("""{"success":7}""");
        await Assert.That(failureJson).IsEqualTo("""{"failure":"invalid"}""");
        await Assert.That(
            JsonSerializer.Deserialize<Result<int, string>>(successJson, Options) is Success<int> { Value: 7 }).IsTrue();
        await Assert.That(
            JsonSerializer.Deserialize<Result<int, string>>(failureJson, Options) is Failure<string> { Value: "invalid" }).IsTrue();
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
}
