using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace GymFlow.Application.Invitations;

public static class InvitationTokens
{
    public static (string Token, string Hash) Create()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
