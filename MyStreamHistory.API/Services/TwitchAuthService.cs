namespace MyStreamHistory.API.Services
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Text.Json;
    using Newtonsoft.Json;

    public class TwitchAuthService : ITwitchAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _uri;
        private readonly string _redirectUri;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public TwitchAuthService(IHttpClientFactory httpClientFactory, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;

            _clientId = _configuration.GetValue<string>("Twitch:ClientId") ?? "";
            _clientId = _clientId?.Replace("%TWITCH_CLIENT_ID%", Environment.GetEnvironmentVariable("TWITCH_CLIENT_ID")) ?? "";
            
            
            _clientSecret = _configuration.GetValue<string>("Twitch:ClientSecret") ?? "";
            _clientSecret = _clientSecret?.Replace("%TWITCH_CLIENT_SECRET%", Environment.GetEnvironmentVariable("TWITCH_CLIENT_SECRET")) ?? "";
            _environment = environment;

            if (_environment.IsDevelopment())
            {
                _uri = "https://localhost:5000";
            }
            else
            {
                _uri = "https://api.mystreamhistory.com";
            }
            _redirectUri = $"{_uri}/auth/twitch/token";
        }

        public async Task<TokenResponse> ExchangeCodeForTokenAsync(string code)
        {
            var client = _httpClientFactory.CreateClient();
            var request = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("redirect_uri", _redirectUri)
            });

            var respone = await client.PostAsync("https://id.twitch.tv/oauth2/token", request);
            if (!respone.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await respone.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TokenResponse>(content);
        }

        public async Task<TwitchUser> GetUserInfoAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            client.DefaultRequestHeaders.Add("Client-Id", _clientId);

            var response = await client.GetAsync("https://api.twitch.tv/helix/users");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var userData = JsonConvert.DeserializeObject<TwitchUserResponse>(content);
            return userData?.Data?.FirstOrDefault();
        }
    }

    public class TokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("scope")]
        public string[] Scope { get; set; } = Array.Empty<string>();

        [JsonProperty("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }
    public class TwitchUser
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("email")]
        public string? Email { get; set; }
        [JsonProperty("login")]
        public string Login { get; set; } = string.Empty;
        [JsonProperty("display_name")]
        public string DisplayName { get; set; } = string.Empty;
        [JsonProperty("profile_image_url")]
        public string ProfileImageUrl { get; set; } = string.Empty;
        [JsonProperty("broadcaster_type")]
        public string BroadcasterType { get; set; } = string.Empty;
        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
    public class TwitchUserResponse
    {
        public List<TwitchUser> Data { get; set; } = new();
    }
}
