using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace P26_002_Pultral.Services
{
    public class GeniusClientOptions
    {
        public string BaseUrl { get; set; } = "https://pultrall.geniuserpcloud.com:53215/api/";
        public string? TokenEndpoint { get; set; } = "https://pultrall.geniuserpcloud.com:53225/connect/token";
        public string? ClientId { get; set; } = "33d87491-dbca-4cbc-8b80-7b53323098a3";
        public string? ClientSecret { get; set; } = "iJES7R#XVeU&JLlr1K6oq1ev3UZ0L6";
        public string? GrantType { get; set; } = "client_credentials";
        public string? Scope { get; set; } = "openid offline_access public";
        public string? XsrfHeaderName { get; set; } = "X-XSRF-TOKEN";
        public string? XsrfCookieName { get; set; } = "XSRF-TOKEN";
        public int TokenCacheMinutes { get; set; } = 30;
        public bool UseXsrfOnTokenRequest { get; set; } = true;

        // Cookie-based auth bootstrap (optional)
        public Dictionary<string, string>? InitialCookies { get; set; } // key: cookie name, value: cookie value
        public string? CookieDomain { get; set; }
    }

    // Singleton service for calling the client's HTTP API with cached tokens/cookies
    public class ClientApiService : IDisposable
    {
        private const string TokenCacheKey = "ClientApi:AccessToken";
        private const string RefreshTokenCacheKey = "ClientApi:RefreshToken";
        private const string XsrfCacheKey = "ClientApi:XsrfToken";

        private readonly ILogger<ClientApiService> _logger;
        private readonly IMemoryCache _cache;
        private readonly GeniusClientOptions _options;
        private readonly CookieContainer _cookies;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public ClientApiService(IOptions<GeniusClientOptions> options, IMemoryCache cache, ILogger<ClientApiService> logger)
        {
            _logger = logger;
            _cache = cache;
            _options = options.Value;

            _cookies = new CookieContainer();
            var handler = new SocketsHttpHandler
            {
                UseCookies = true,
                CookieContainer = _cookies,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10)
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = string.IsNullOrWhiteSpace(_options.BaseUrl) ? null : new Uri(_options.BaseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(100)
            };

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Seed cookies/XSRF from configuration if provided (useful for local dev)
            TrySeedCookiesFromOptions();
        }

        public async Task<HttpResponseMessage> GetAsync(string relativeOrAbsoluteUrl, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(relativeOrAbsoluteUrl));
            await EnrichAuthAsync(request, ct);
            var response = await _httpClient.SendAsync(request, ct);
            await HandleAuthFailuresAsync(response);
            return response;
        }

        public async Task<HttpResponseMessage> PostJsonAsync<T>(string relativeOrAbsoluteUrl, T payload, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(relativeOrAbsoluteUrl))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            await EnrichAuthAsync(request, ct);
            var response = await _httpClient.SendAsync(request, ct);
            await HandleAuthFailuresAsync(response);
            return response;
        }

        private Uri BuildUri(string relativeOrAbsolute)
        {
            if (Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var abs))
                return abs;
            if (_httpClient.BaseAddress is null)
                throw new InvalidOperationException("ClientApiService BaseUrl is not configured, and a relative URL was provided.");
            return new Uri(_httpClient.BaseAddress, relativeOrAbsolute);
        }

        private async Task EnrichAuthAsync(HttpRequestMessage request, CancellationToken ct)
        {
            // Bearer token (cached)
            var token = await GetAccessTokenAsync(ct);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // XSRF header if we have it
            if (!string.IsNullOrWhiteSpace(_options.XsrfHeaderName) &&
                _cache.TryGetValue<string>(XsrfCacheKey, out var xsrf))
            {
                request.Headers.TryAddWithoutValidation(_options.XsrfHeaderName!, xsrf);
            }

            // Add CompanyCode header
            request.Headers.TryAddWithoutValidation("CompanyCode", "PULTRA");
        }

        private async Task HandleAuthFailuresAsync(HttpResponseMessage response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                _cache.Remove(TokenCacheKey);
                _cache.Remove(RefreshTokenCacheKey);
                _cache.Remove(XsrfCacheKey);
                _logger.LogWarning("Client API returned {Status}. Cleared cached auth, refresh, and xsrf tokens.", response.StatusCode);
            }

            // Optionally capture XSRF cookie from response for next calls
            if (!string.IsNullOrWhiteSpace(_options.XsrfCookieName))
            {
                try
                {
                    var baseUri = _httpClient.BaseAddress ?? response.RequestMessage?.RequestUri;
                    if (baseUri != null)
                    {
                        var cookies = _cookies.GetCookies(new Uri(baseUri.GetLeftPart(UriPartial.Authority)));
                        foreach (Cookie cookie in cookies)
                        {
                            if (string.Equals(cookie.Name, _options.XsrfCookieName, StringComparison.OrdinalIgnoreCase))
                            {
                                _cache.Set(XsrfCacheKey, cookie.Value, TimeSpan.FromMinutes(_options.TokenCacheMinutes));
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to read XSRF cookie from response.");
                }
            }

            await Task.CompletedTask;
        }

        private async Task<string?> GetAccessTokenAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue<string>(TokenCacheKey, out var token) && !string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            // Try to refresh using refresh token first
            if (_cache.TryGetValue<string>(RefreshTokenCacheKey, out var refreshToken) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                var (refreshedToken, refreshExpiresIn, newRefreshToken) = await RefreshAccessTokenAsync(refreshToken, ct);
                if (!string.IsNullOrWhiteSpace(refreshedToken))
                {
                    CacheTokens(refreshedToken, refreshExpiresIn, newRefreshToken);
                    return refreshedToken;
                }
                
                // Refresh failed, clear the invalid refresh token
                _cache.Remove(RefreshTokenCacheKey);
                _logger.LogInformation("Refresh token invalid or expired, falling back to client credentials.");
            }

            // Fall back to client credentials flow
            var (accessToken, expiresIn, refreshTokenNew) = await RequestNewAccessTokenAsync(ct);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                CacheTokens(accessToken, expiresIn, refreshTokenNew);
            }

            return accessToken;
        }

        private void CacheTokens(string accessToken, int expiresIn, string? refreshToken)
        {
            // Use expires_in from response, subtract 5 seconds for safety
            var cacheDuration = expiresIn > 5 
                ? TimeSpan.FromSeconds(expiresIn - 5) 
                : TimeSpan.FromMinutes(_options.TokenCacheMinutes);
            
            _cache.Set(TokenCacheKey, accessToken, cacheDuration);

            // Cache refresh token with a longer duration (typically doesn't expire, but cache for safety)
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                _cache.Set(RefreshTokenCacheKey, refreshToken, TimeSpan.FromHours(24));
            }
        }

        private async Task<(string? accessToken, int expiresIn, string? refreshToken)> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_options.TokenEndpoint))
                {
                    return (null, 0, null);
                }

                // Build OAuth2 refresh token payload
                var formData = new Dictionary<string, string>
                {
                    { "client_id", _options.ClientId ?? string.Empty },
                    { "client_secret", _options.ClientSecret ?? string.Empty },
                    { "grant_type", "refresh_token" },
                    { "refresh_token", refreshToken }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(_options.TokenEndpoint))
                {
                    Content = new FormUrlEncodedContent(formData)
                };

                req.Headers.Accept.Clear();
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var res = await _httpClient.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Token refresh failed with status {Status}", res.StatusCode);
                    return (null, 0, null);
                }

                using var stream = await res.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                return ParseTokenResponse(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while refreshing access token.");
                return (null, 0, null);
            }
        }

        private async Task<(string? accessToken, int expiresIn, string? refreshToken)> RequestNewAccessTokenAsync(CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_options.TokenEndpoint))
                {
                    // No token endpoint configured; return null and rely on API key/cookies
                    return (null, 0, null);
                }

                // Build OAuth2 client credentials payload
                var formData = new Dictionary<string, string>
                {
                    { "client_id", _options.ClientId ?? string.Empty },
                    { "client_secret", _options.ClientSecret ?? string.Empty },
                    { "grant_type", _options.GrantType ?? "client_credentials" },
                    { "scope", _options.Scope ?? "openid offline_access public" }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(_options.TokenEndpoint))
                {
                    Content = new FormUrlEncodedContent(formData)
                };

                req.Headers.Accept.Clear();
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // XSRF for token request if configured
                if (_options.UseXsrfOnTokenRequest && !string.IsNullOrWhiteSpace(_options.XsrfHeaderName) &&
                    _cache.TryGetValue<string>(XsrfCacheKey, out var xsrf))
                {
                    req.Headers.TryAddWithoutValidation(_options.XsrfHeaderName!, xsrf);
                }

                var res = await _httpClient.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Token request failed with status {Status}", res.StatusCode);
                    return (null, 0, null);
                }

                using var stream = await res.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

                return ParseTokenResponse(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while requesting new access token.");
                return (null, 0, null);
            }
        }

        private (string? accessToken, int expiresIn, string? refreshToken) ParseTokenResponse(JsonDocument doc)
        {
            // OAuth2 standard returns { access_token: "...", expires_in: 3600, refresh_token: "..." }
            string? accessToken = null;
            int expiresIn = 0;
            string? refreshToken = null;

            if (doc.RootElement.TryGetProperty("access_token", out var accessTokenElement))
            {
                accessToken = accessTokenElement.GetString();
            }

            if (doc.RootElement.TryGetProperty("expires_in", out var expiresInElement))
            {
                expiresIn = expiresInElement.GetInt32();
            }

            if (doc.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement))
            {
                refreshToken = refreshTokenElement.GetString();
            }

            return (accessToken, expiresIn, refreshToken);
        }

        private void TrySeedCookiesFromOptions()
        {
            try
            {
                if (_options.InitialCookies == null || _options.InitialCookies.Count == 0)
                    return;

                var baseUri = _httpClient.BaseAddress;
                string? domain = _options.CookieDomain;
                if (string.IsNullOrWhiteSpace(domain))
                {
                    domain = baseUri?.Host;
                }

                if (string.IsNullOrWhiteSpace(domain))
                {
                    _logger.LogWarning("Cannot seed cookies: no CookieDomain and BaseUrl is not configured.");
                    return;
                }

                // Build a valid URI to add cookies to. If domain starts with '.', trim it for the URI host.
                var uriHost = domain.StartsWith('.') ? domain.TrimStart('.') : domain;
                var targetUri = baseUri ?? new Uri($"https://{uriHost}");

                foreach (var kvp in _options.InitialCookies)
                {
                    var name = kvp.Key;
                    var value = kvp.Value;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                        continue;

                    var cookie = new Cookie(name, value)
                    {
                        Domain = domain,
                        Path = "/",
                        HttpOnly = string.Equals(name, ".AspNetCore.Identity.Application", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".AspNetCore.Antiforgery", StringComparison.OrdinalIgnoreCase),
                        Secure = true
                    };
                    _cookies.Add(targetUri, cookie);

                    // If this is the XSRF cookie, also cache it for header usage
                    if (!string.IsNullOrWhiteSpace(_options.XsrfCookieName) &&
                        string.Equals(name, _options.XsrfCookieName, StringComparison.OrdinalIgnoreCase))
                    {
                        _cache.Set(XsrfCacheKey, value, TimeSpan.FromMinutes(_options.TokenCacheMinutes));
                    }
                }

                _logger.LogInformation("Seeded {Count} initial cookies for domain {Domain}", _options.InitialCookies.Count, domain);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to seed initial cookies from options.");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _httpClient.Dispose();
        }
    }
}
