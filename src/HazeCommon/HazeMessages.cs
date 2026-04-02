using System.Security.Cryptography;

namespace HazeCommon;

public static class HazeMessages
{
    private const string MessageIdCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

    public static string MakeMessageId() => RandomNumberGenerator.GetString(MessageIdCharacters, 10);
}
