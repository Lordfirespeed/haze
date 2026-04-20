namespace Haze.Steam;

public class SteamTokenSet(string accessToken, string refreshToken)
{
    public string AccessToken => accessToken;
    public string RefreshToken => refreshToken;
}
