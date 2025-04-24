namespace MyStreamHistory.API.Services
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Text.Json.Serialization;
    using System.Diagnostics;
    using MyStreamHistory.API.Models;
    using MyStreamHistory.API.Repositories;
    using Newtonsoft.Json;

    public class TwitchAuthService : ITwitchAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _uri;
        private readonly string _redirectUri;
        private readonly IWebHostEnvironment _environment;

        public TwitchAuthService(IHttpClientFactory httpClientFactory, IWebHostEnvironment environment)
        {
            _httpClientFactory = httpClientFactory;
            _clientId = "a77bf3umj99gay4n0ng8k5u70qsqja";
            _clientSecret = "lmowevzqw3l85fn3g9vhe3vc23fmi6";
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
            Debug.WriteLine("Test");
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
            var rText = await respone.Content.ReadAsStringAsync();
            Debug.WriteLine(rText);
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
            Debug.WriteLine(content);
            return userData?.Data?.FirstOrDefault();
        }
    }

    public class TokenResponse
    {
        [JsonProperty("access_token")]
        public string AcessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
    }
    public class TwitchUser
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("login")]
        public string Login { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("profile_image_url")]
        public string ProfileImageUrl { get; set; }

        [JsonProperty("broadcaster_type")]
        public string BroadcasterType { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }
    public class TwitchUserResponse
    {
        [JsonProperty("data")]
        public List<TwitchUser> Data { get; set; }
    }
}