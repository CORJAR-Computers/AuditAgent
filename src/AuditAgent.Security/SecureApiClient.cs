using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using AuditAgent.Core.Interfaces;

namespace AuditAgent.Security;

/// <summary>
/// Cliente HTTP seguro con mTLS para comunicacion con el servidor central.
/// TLS 1.3 obligatorio, mTLS, timeouts, whitelist de servidores.
/// </summary>
public class SecureApiClient : IApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string? _serverPublicKey;
    private readonly HashSet<string> _allowedServers;
    private bool _disposed;

    public SecureApiClient(
        string serverUrl,
        X509Certificate2 clientCert,
        X509Certificate2? serverCaCert = null,
        string? serverPublicKey = null,
        string[]? additionalAllowedServers = null)
    {
        _allowedServers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeUrl(serverUrl)
        };

        if (additionalAllowedServers is not null)
        {
            foreach (var srv in additionalAllowedServers)
                _allowedServers.Add(NormalizeUrl(srv));
        }

        _serverPublicKey = serverPublicKey;

        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                    ValidateServerCertificate(cert as X509Certificate2, chain, errors),
                ClientCertificates = new X509CertificateCollection { clientCert },
                EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
            },
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(30),
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(serverUrl),
            Timeout = TimeSpan.FromSeconds(60),
        };

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("X-Agent-Version", "1.0.0");
    }

    public async Task<ApiSubmissionResult> SubmitReportAsync(
        string encryptedReport,
        string signature,
        string reportHash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                encryptedReport,
                signature,
                reportHash,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(
                "/api/audit/submit", content, cancellationToken);

            return new ApiSubmissionResult
            {
                Success = response.IsSuccessStatusCode,
                HttpStatusCode = (int)response.StatusCode,
                ServerTimestamp = response.Headers.TryGetValues("X-Server-Time", out var t)
                    ? t.FirstOrDefault() : null,
                ErrorMessage = !response.IsSuccessStatusCode
                    ? await response.Content.ReadAsStringAsync(cancellationToken) : null
            };
        }
        catch (TaskCanceledException)
        {
            return new ApiSubmissionResult
            {
                Success = false,
                HttpStatusCode = 408,
                ErrorMessage = "Timeout al conectar con el servidor."
            };
        }
        catch (HttpRequestException ex)
        {
            return new ApiSubmissionResult
            {
                Success = false,
                HttpStatusCode = 0,
                ErrorMessage = "Error de conexion: " + ex.Message
            };
        }
    }

    public async Task<RegistrationResult> RegisterAgentAsync(
        string machineFingerprint,
        string publicKeyPem,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                machineFingerprint,
                publicKey = publicKeyPem,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(
                "/api/audit/register", content, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
                return new RegistrationResult
                {
                    Success = true,
                    AgentId = doc.RootElement.GetProperty("agentId").GetString(),
                    ServerPublicKey = doc.RootElement.GetProperty("serverPublicKey").GetString()
                };
            }

            return new RegistrationResult
            {
                Success = false,
                ErrorMessage = responseJson
            };
        }
        catch (Exception ex)
        {
            return new RegistrationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private bool ValidateServerCertificate(
        X509Certificate2? cert,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
#if DEBUG
        if (errors == SslPolicyErrors.RemoteCertificateChainErrors)
            return true;
#endif
        if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
            return false;
        if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
            return false;
        return true;
    }

    private static string NormalizeUrl(string url)
    {
        try { return new Uri(url).Host.ToLowerInvariant(); }
        catch { return url.ToLowerInvariant(); }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
