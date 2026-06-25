using BenchmarkDotNet.Running;
using TeleFlow.Benchmarks;

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, BenchmarkConfiguration.Create());
