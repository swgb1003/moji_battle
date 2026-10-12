using System;

namespace MojiBattle
{
    public static class FriendRoomServerConfig
    {
        public const string LocalUrl = "http://127.0.0.1:8787";

        public static string PublicUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                uri.IsLoopback || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment)) return null;
            return uri.AbsoluteUri.TrimEnd('/');
        }

        /// <summary>WebGL のページの URL から、同じドメインの中継先（/rooms-api）を返す。https のページ以外では null。</summary>
        public static string RelayUrl(string pageUrl)
        {
            if (string.IsNullOrWhiteSpace(pageUrl) || !Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https"
                || uri.IsLoopback) return null;
            return uri.GetLeftPart(UriPartial.Authority) + "/rooms-api";
        }

        public static string Resolve(string bundledUrl, string savedUrl)
        {
            // A previously saved localhost address must not override the shipped public endpoint.
            return PublicUrl(bundledUrl) ?? (string.IsNullOrWhiteSpace(savedUrl) ? LocalUrl : savedUrl);
        }
    }
}
