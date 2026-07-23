using AuditAgent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar Kestrel con HTTPS y certificado de servidor
builder.WebHost.ConfigureKestrel(options =>
{
    // Configurar certificado del servidor
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
builder.Services.AddSingleton<AuditStorageService>();
builder.Services.AddSingleton<EncryptionKeyService>();

// CORS para el dashboard frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy =>
    {
        policy.WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "https://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Swagger para desarrollo
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Dashboard");
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
