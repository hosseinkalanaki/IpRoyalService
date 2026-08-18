using IpRoyalService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = ServiceIdentity.Name);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.TimestampFormat = "O");
builder.Logging.AddProvider(new JsonFileLoggerProvider(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "IpRoyalService", "service.log")));
builder.Services.AddSingleton<ConfigLoader>();
builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
builder.Services.AddSingleton<SingBoxConfigWriter>();
builder.Services.AddSingleton<ProxyProbe>();
builder.Services.AddSingleton<IProxyPathProbe>(sp => sp.GetRequiredService<ProxyProbe>());
builder.Services.AddSingleton(new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) });
builder.Services.AddSingleton<IProxyRouteSwitcher, ProxyRouteSwitcher>();
builder.Services.AddSingleton<AutomaticProxySelector>();
builder.Services.AddSingleton<EnforcementController>();
builder.Services.AddHostedService<ProxyEnforcementWorker>();
await builder.Build().RunAsync();
