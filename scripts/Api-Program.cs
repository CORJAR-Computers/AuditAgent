using AuditAgent.Api.Services;
using AuditAgent.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configurar Kestrel con HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    var certPath = builder.Configuration["Server:CertPath"];
    var certPassword = builder.Configuration["Server:CertPassword"];
    if (!string.IsNullOrEmpty(certPath))
    {
        options.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                certPath, certPassword);
            httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls13 |
                                          System.Security.Authentication.SslProtocols.Tls12;
        });
    }
});

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();  // FIX CRITICO: requerido por AuditStorageService.GetClientIp()
builder.Services.AddSingleton<AuditStorageService>();
builder.Services.AddSingleton<EncryptionKeyService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy =>
    {
        policy.WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "https://localhost:3000")
              .AllowAnyHeader().AllowAnyMethod();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// FIX: Autenticacion por API Key para endpoints POST
var apiKey = builder.Configuration["Security:ApiKey"];
if (!string.IsNullOrEmpty(apiKey))
{
    app.UseApiKeyAuthentication(apiKey);
}

app.UseCors("Dashboard");
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
