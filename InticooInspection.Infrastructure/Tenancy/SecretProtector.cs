using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace InticooInspection.Infrastructure.Tenancy;

/// <summary>Mã hóa chuỗi kết nối DB của từng khách (AES-256-GCM). Khóa nằm ngoài DB (secrets/biến môi trường).</summary>
public class SecretProtector
{
    private readonly byte[] _key;
    public SecretProtector(IConfiguration cfg)
    {
        var raw = cfg["Security:TenantSecretKey"];
        if (string.IsNullOrWhiteSpace(raw) || raw.Length < 32)
            throw new InvalidOperationException("Thiếu Security:TenantSecretKey (>= 32 ký tự).");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
    }

    public string Protect(string plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[data.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(_key, 16);
        gcm.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public string Unprotect(string protectedText)
    {
        var all = Convert.FromBase64String(protectedText);
        var nonce = all[..12]; var tag = all[12..28]; var cipher = all[28..];
        var plain = new byte[cipher.Length];
        using var gcm = new AesGcm(_key, 16);
        gcm.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
