using UltimaMilla.Application;
using UltimaMilla.Infrastructure;
using UltimaMilla.Worker;
using UltimaMilla.Worker.BackgroundServices;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.Seccion));

// El worker reutiliza los casos de uso y los puertos de la aplicación; no contiene lógica de negocio propia
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
builder.Services.AddHostedService<EventConsumerBackgroundService>();
builder.Services.AddHostedService<ScheduledJobsBackgroundService>();

var host = builder.Build();
host.Run();
