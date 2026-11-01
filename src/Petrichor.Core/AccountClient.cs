using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Petrichor.Core
{
    public sealed partial class NeteaseClient
    {
        public Task RemoveTrackFromPlaylistAsync(long playlistId, long trackId, CancellationToken ct = default(CancellationToken))
            => RemoveTracksFromPlaylistAsync(playlistId, new[] { trackId }, ct);

        public async Task RemoveTracksFromPlaylistAsync(long playlistId, IEnumerable<long> trackIds, CancellationToken ct = default(CancellationToken))
        {
            RequireLogin(); ValidId(playlistId);
            var ids = (trackIds ?? throw new ArgumentNullException(nameof(trackIds))).Select(ValidId).Distinct().ToList();
            if (ids.Count == 0) throw new ArgumentException("请先选择歌曲", nameof(trackIds));
            var playlist = await GetPlaylistAsync(playlistId, ct).ConfigureAwait(false);
            if (playlist.Creator == null || playlist.Creator.Id != CurrentUser?.Id)
                throw new MusicApiException(403, "只能移除自己创建的歌单中的歌曲");
            if (playlist.SpecialType == 5) {
                foreach (var id in ids) await SetLikedAsync(id, false, ct).ConfigureAwait(false);
                return;
            }
            for (int offset = 0; offset < ids.Count; offset += 100) {
                ct.ThrowIfCancellationRequested();
                await RequestAsync("playlist/manipulate/tracks", Data("op", "del", "pid", playlistId,
                    "trackIds", JsonConvert.SerializeObject(ids.Skip(offset).Take(100)), "imme", "true"), ct).ConfigureAwait(false);
            }
        }
        private static string ValidatePhone(string phone, string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode) || !Regex.IsMatch(countryCode, @"^[1-9][0-9]{0,3}$"))
                throw new ArgumentException("请输入正确的国家区号", nameof(countryCode));
            var value = Regex.Replace(phone ?? "", @"[\s-]", "");
            if (!Regex.IsMatch(value, @"^[0-9]{5,15}$") || (countryCode == "86" && !Regex.IsMatch(value, @"^1[0-9]{10}$")))
                throw new ArgumentException("请输入正确的手机号码", nameof(phone));
            return value;
        }

        public async Task SendLoginCodeAsync(string phone, string countryCode = "86", CancellationToken ct = default(CancellationToken))
        {
            var number = ValidatePhone(phone, countryCode);
            var j = await RequestAsync("sms/captcha/sent", Data("cellphone", number, "ctcode", countryCode,
                "secrete", "music_middleuser_pclogin"), ct).ConfigureAwait(false);
            if (j["data"]?.Type == JTokenType.Boolean && !(bool)j["data"])
                throw new MusicApiException(502, "验证码未发送，请稍后重试");
        }

        public async Task<UserProfile> LoginWithCodeAsync(string phone, string code, string countryCode = "86", CancellationToken ct = default(CancellationToken))
        {
            var number = ValidatePhone(phone, countryCode);
            if (!Regex.IsMatch(code ?? "", @"^[0-9]{4,8}$")) throw new ArgumentException("请输入短信中的验证码", nameof(code));
            await RequestAsync("w/login/cellphone", Data("phone", number, "countrycode", countryCode,
                "captcha", code, "type", "1", "https", "true", "remember", "true"), ct).ConfigureAwait(false);
            return await VerifyLoginSessionAsync(ct).ConfigureAwait(false);
        }

        /// <summary>Logs in with a phone number or NetEase email. Only the session is persisted.</summary>
        public async Task<UserProfile> LoginWithPasswordAsync(string account, string password, string countryCode = "86", CancellationToken ct = default(CancellationToken))
        {
            ct.ThrowIfCancellationRequested();
            var value = (account ?? "").Trim();
            var email = value.Contains("@");
            if (email) {
                if (!Regex.IsMatch(value, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                    throw new ArgumentException("请输入正确的网易邮箱地址", nameof(account));
            } else value = ValidatePhone(value, countryCode);
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("请输入密码", nameof(password));
            if (eapiProvider == null) throw new NotSupportedException("当前平台未提供密码登录所需的加密功能");
            // The upstream protocol requires UTF-8 MD5 inside the existing encrypted HTTPS request.
            // Preserve spaces and case in the password; never persist the password or its digest.
            var digest = BitConverter.ToString(eapiProvider.Md5(Encoding.UTF8.GetBytes(password))).Replace("-", "").ToLowerInvariant();
            var data = email
                ? Data("username", value, "password", digest, "type", "0", "https", "true", "rememberLogin", "true")
                : Data("phone", value, "countrycode", countryCode, "password", digest, "type", "1", "https", "true", "remember", "true");
            await RequestAsync(email ? "w/login" : "w/login/cellphone", data, ct).ConfigureAwait(false);
            return await VerifyLoginSessionAsync(ct).ConfigureAwait(false);
        }

        private async Task<UserProfile> VerifyLoginSessionAsync(CancellationToken ct)
        {
            // A 200 response is not sufficient: require a usable session and verify the account.
            if (cookies.GetCookies(Origin)["MUSIC_U"] == null)
                throw new MusicApiException(502, "网易云未返回登录凭据，请重试或使用扫码登录");
            return await GetCurrentUserAsync(ct).ConfigureAwait(false);
        }

        public async Task<Playlist> CreatePlaylistAsync(string name, bool isPrivate = false, CancellationToken ct = default(CancellationToken))
        {
            RequireLogin();
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 40)
                throw new ArgumentException("歌单名称需为 1 至 40 个字", nameof(name));
            var j = await RequestAsync("playlist/create", Data("name", name.Trim(), "privacy", isPrivate ? 10 : 0, "type", "NORMAL"), ct).ConfigureAwait(false);
            var token = j["playlist"];
            if (token == null || token.Type == JTokenType.Null || ((long?)token["id"] ?? 0) <= 0)
                throw new MusicApiException(502, "网易云未返回新歌单，请刷新列表确认后重试");
            return Playlist.From(token);
        }

        public async Task SetPlaylistSubscribedAsync(long id, bool subscribed, CancellationToken ct = default(CancellationToken))
        {
            RequireLogin();
            await RequestAsync("playlist/" + (subscribed ? "subscribe" : "unsubscribe"), Data("id", ValidId(id)), ct).ConfigureAwait(false);
        }
    }
}
