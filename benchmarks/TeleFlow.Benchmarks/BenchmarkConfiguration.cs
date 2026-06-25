using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Loggers;

namespace TeleFlow.Benchmarks;

internal static class BenchmarkConfiguration
{
    public static IConfig Create()
    {
        return ManualConfig
            .Create(DefaultConfig.Instance)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddColumn(CategoriesColumn.Default)
            .AddColumn(RankColumn.Arabic)
            .AddExporter(JsonExporter.Full)
            .AddLogger(ConsoleLogger.Default);
    }
}
