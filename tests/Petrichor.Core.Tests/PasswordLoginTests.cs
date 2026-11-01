using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Petrichor.Core;

internal static class PasswordLoginTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var fake = new FakeHandler();
        var store = new MemorySessionStore();
        using var client = new NeteaseClient(new DesktopCrypto(), store, fake);
        foreach (var input in new[] { "", "123", "name@", "name @163.com" }) {
            try { await client.LoginWithPasswordAsync(input, "test"); throw new Exception("Invalid account accepted"); }
            catch (ArgumentException) { check(fake.LastUri == null, "Invalid password-login account rejected before network: " + input); }
        }
        try { await client.LoginWithPasswordAsync("13800138000", ""); throw new Exception("Empty password accepted"); }
        catch (ArgumentException) { check(fake.LastUri == null, "Empty password rejected before network"); }

        // This is a synthetic value used only with the in-memory HTTP handler.
        const string password = " 密码Aa1! ";
        var expected = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
        fake.Bodies.Enqueue("{\"code\":200}"); fake.LoginCookie = true;
        fake.Bodies.Enqueue("{\"code\":200,\"profile\":{\"userId\":42,\"nickname\":\"测试\"}}");
        await client.LoginWithPasswordAsync(" 138 0013-8000 ", password);
        var payload = fake.Payloads[0];
        check((string)payload["phone"] == "13800138000" && (string)payload["countrycode"] == "86" && (string)payload["type"] == "1" && payload["captcha"] == null,
            "Phone password login normalizes phone and sends password protocol fields without captcha");
        check((string)payload["password"] == expected && !payload.ToString().Contains(password), "Password uses lowercase UTF-8 MD5 and preserves spaces and case inside encrypted request");
        check(client.CurrentUser.Id == 42 && fake.LastUri.AbsolutePath == "/weapi/w/nuser/account/get" && (await store.LoadAsync()).Contains("MUSIC_U="), "Password login verifies session with account endpoint before success");
        check(!(await store.LoadAsync()).Contains(password) && !(await store.LoadAsync()).Contains(expected), "Only session cookies are persisted, never password or digest");
        await client.LogoutAsync();

        fake.Bodies.Enqueue("{\"code\":200}");
        try { await client.LoginWithPasswordAsync("user@163.com", password); throw new Exception("Cookie-less login accepted"); }
        catch (MusicApiException e) { check(e.Code == 502 && !client.IsLoggedIn && fake.LastUri.AbsolutePath == "/weapi/w/login", "Email password login uses email endpoint and rejects missing session cookie"); }
        check((string)fake.LastPayload["username"] == "user@163.com" && (string)fake.LastPayload["password"] == expected && (string)fake.LastPayload["type"] == "0" && (string)fake.LastPayload["rememberLogin"] == "true" && fake.LastPayload["phone"] == null,
            "Email login sends distinct username and rememberLogin fields");

        fake.Bodies.Enqueue("{\"code\":200}"); fake.LoginCookie = true;
        fake.Bodies.Enqueue("{\"code\":200,\"profile\":{\"userId\":43,\"nickname\":\"邮箱用户\"}}");
        await client.LoginWithPasswordAsync(" user@163.com ", password, "invalid-unused-country");
        check(client.CurrentUser.Id == 43 && (string)fake.Payloads[fake.Payloads.Count - 2]["username"] == "user@163.com", "Email login trims account and does not require phone country code");
        await client.LogoutAsync();

        fake.Bodies.Enqueue("{\"code\":200}");
        try { await client.LoginWithPasswordAsync("4155550123", "test", "1"); throw new Exception("Cookie-less login accepted"); }
        catch (MusicApiException) { check((string)fake.LastPayload["countrycode"] == "1" && (string)fake.LastPayload["phone"] == "4155550123", "International phone password login preserves country code"); }
        foreach (var error in new[] { 502, 415 }) {
            fake.Bodies.Enqueue("{\"code\":" + error + ",\"message\":\"服务端验证提示\"}");
            try { await client.LoginWithPasswordAsync("13800138000", "test"); throw new Exception("Failed login accepted"); }
            catch (MusicApiException e) { check(e.Code == error && e.Message == "服务端验证提示" && !client.IsLoggedIn && string.IsNullOrEmpty(await store.LoadAsync()), "Password errors and additional-verification errors propagate without false login: " + error); }
        }
        fake.Bodies.Enqueue("{\"code\":200}"); fake.LoginCookie = true;
        fake.Bodies.Enqueue("{\"code\":200,\"profile\":null}");
        try { await client.LoginWithPasswordAsync("13800138000", "test"); throw new Exception("Missing profile accepted"); }
        catch (MusicApiException e) { check(e.RequiresLogin && !client.IsLoggedIn && string.IsNullOrEmpty(await store.LoadAsync()), "Unverifiable password session is cleared instead of accepted"); }
        var calls = fake.Payloads.Count;
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await client.LoginWithPasswordAsync("13800138000", "test", ct: cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { check(fake.Payloads.Count == calls, "Cancelled password login sends no request"); }
    }
}
