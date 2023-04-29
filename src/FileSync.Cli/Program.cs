using Serilog;
using FileSync.Domain.Abstractions;
using FileSync.Domain.Strategies;
using FileSync.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO.Abstractions;
using CommandLine;
using FileSync.Cli.Index;
using FileSync.Cli.Synchronize;
using FileSync.Cli.Verify;

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

var hostBuilder = Host.CreateDefaultBuilder();
hostBuilder.UseSerilog();
hostBuilder.ConfigureServices(services =>
{
    services.AddScoped<IDirectoryEnumerator, DirectoryEnumerator>();
    services.AddSingleton<IIndexWriter, IndexWriter>();
    services.AddSingleton<IEntryComparer, MetadataEntryComparer>();
    services.AddSingleton<IIncrementalSynchronizer, IncrementalSynchronizer>();
    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddSingleton<IChecksumGenerator, Md5ChecksumGenerator>();
    services.AddSingleton<IDiffWriter, DiffWriter>();

    services.AddSingleton<SynchronizeApp>();
    services.AddSingleton<ReconcileApp>();
    services.AddSingleton<VerifyApp>();
});

var app = hostBuilder.Build();

var parserResult = Parser.Default.ParseArguments<SynchronizeOptions, ReconcileOptions, VerifyOptions, IndexOptions>(args);

await parserResult.WithParsedAsync<SynchronizeOptions>(options => app.Services.GetRequiredService<SynchronizeApp>().RunAsync(options));
await parserResult.WithParsedAsync<ReconcileOptions>(options => app.Services.GetRequiredService<ReconcileApp>().RunAsync(options));
await parserResult.WithParsedAsync<VerifyOptions>(options => app.Services.GetRequiredService<VerifyApp>().RunAsync(options));
await parserResult.WithParsedAsync<IndexOptions>(options => app.Services.GetRequiredService<IndexApp>().RunAsync(options));
