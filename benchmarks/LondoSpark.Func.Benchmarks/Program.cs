using BenchmarkDotNet.Running;

using LondoSpark.Func.Benchmarks;

BenchmarkSwitcher
    .FromAssembly(typeof(OptionCompositionBenchmarks).Assembly)
    .Run(args);
