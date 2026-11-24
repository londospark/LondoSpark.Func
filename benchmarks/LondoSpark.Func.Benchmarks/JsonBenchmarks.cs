using System.Text.Json;
using BenchmarkDotNet.Attributes;

using LondoSpark.Func.Json;

namespace LondoSpark.Func.Benchmarks;

/// <summary>Measures the converters against serializing the bare payload, which is the floor they add to.</summary>
[MemoryDiagnoser]
public class JsonBenchmarks
{
    private static readonly JsonSerializerOptions Options =
        new JsonSerializerOptions().AddOptionConverter<Payload>().AddResultConverter<Payload, string>();

    private static readonly Payload Value = new(42, "forty-two");
    private static readonly Option<Payload> SomeValue = Option.Return(Value);
    private static readonly Result<Payload, string> Successful = Result.Return<Payload, string>(Value);

    private static readonly byte[] PayloadJson = JsonSerializer.SerializeToUtf8Bytes(Value, Options);
    private static readonly byte[] OptionJson = JsonSerializer.SerializeToUtf8Bytes(SomeValue, Options);
    private static readonly byte[] ResultJson = JsonSerializer.SerializeToUtf8Bytes(Successful, Options);

    [Benchmark(Baseline = true)]
    public byte[] SerializePayload() => JsonSerializer.SerializeToUtf8Bytes(Value, Options);

    [Benchmark]
    public byte[] SerializeOption() => JsonSerializer.SerializeToUtf8Bytes(SomeValue, Options);

    [Benchmark]
    public byte[] SerializeResult() => JsonSerializer.SerializeToUtf8Bytes(Successful, Options);

    [Benchmark]
    public Payload? DeserializePayload() => JsonSerializer.Deserialize<Payload>(PayloadJson, Options);

    [Benchmark]
    public Option<Payload> DeserializeOption() => JsonSerializer.Deserialize<Option<Payload>>(OptionJson, Options);

    [Benchmark]
    public Result<Payload, string> DeserializeResult() => JsonSerializer.Deserialize<Result<Payload, string>>(ResultJson, Options);

    public sealed record Payload(int Id, string Name);
}
