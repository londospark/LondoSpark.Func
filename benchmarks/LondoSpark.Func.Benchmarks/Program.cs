using BenchmarkDotNet.Running;

BenchmarkSwitcher
    .FromAssembly(typeof(OptionCompositionBenchmarks).Assembly)
    .Run(args);
