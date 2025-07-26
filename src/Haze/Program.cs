// See https://aka.ms/new-console-template for more information

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.ConfigureKestrel(options => {
    options.ListenAnyIP(5000);
});
builder.Services.AddControllers();

var app = builder.Build();
app.UseWebSockets(new WebSocketOptions {
    KeepAliveInterval = TimeSpan.FromMinutes(2),
});
app.MapControllers();

var cancellationSource = new CancellationTokenSource();
var cancel = (PosixSignalContext ctx) => cancellationSource.Cancel();
using (PosixSignalRegistration.Create(PosixSignal.SIGINT, cancel))
using (PosixSignalRegistration.Create(PosixSignal.SIGTERM, cancel)) {
    var runTask = app.RunAsync(cancellationSource.Token);
    await runTask;
}
