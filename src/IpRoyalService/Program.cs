using IpRoyalService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = ServiceIdentity.Name);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.TimestampFormat = "O");
builder.Logging.AddProvider(new JsonFileLoggerProvider(ApplicationPaths.LogFile));
builder.Services.AddSingleton<ConfigLoader>();
builder.Services.AddSingleton<ConnectionStatusPublisher>();
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<SingBoxConfigWriter>();
builder.Services.AddSingleton<ProxyProbe>();
builder.Services.AddSingleton<IProxyPathProbe>(sp => sp.GetRequiredService<ProxyProbe>());
builder.Services.AddSingleton<EnforcementController>();
builder.Services.AddHostedService<ProxyEnforcementWorker>();
await builder.Build().RunAsync();
