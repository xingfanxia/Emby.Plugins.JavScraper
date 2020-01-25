﻿using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Plugins.JavScraper.Scrapers
{
    /// <summary>
    /// Js Proxy 客户端
    /// </summary>
    public class JsProxyHttpClientHandler : HttpClientHandler
    {
        /// <summary>
        /// 获取重定向url
        /// </summary>
        private static Regex regexUrl = new Regex(@"href *=["" ]*(?<url>[^"" >]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 发送请求
        /// </summary>
        /// <param name="request">请求信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("X-FORWARDED-FOR", "8.8.8.8");

            if (Plugin.Instance.Configuration.HasJsProxy == false)
                return await base.SendAsync(request, cancellationToken);

            var jsproxy_url = Plugin.Instance.Configuration.JsProxy;
            // Add header to request here
            var url = request.RequestUri.ToString();
            //netcdn 这个域名不走代理
            if (url.StartsWith(jsproxy_url, StringComparison.OrdinalIgnoreCase) != true && request.RequestUri.Host.Contains("netcdn.") == false)
            {
                url = Plugin.Instance.Configuration.BuildProxyUrl(url);
                request.RequestUri = new Uri(url);
                url = request.Headers.Referrer?.ToString();
                if (!(url?.IndexOf("?") > 0))
                    request.Headers.Referrer = new Uri("https://www.google.com/?--ver=110&--mode=navigate&--type=document&origin=&--aceh=1&dnt=1&upgrade-insecure-requests=1&cookie=has_recent_activity%3D1%3B+has_recent_activity%3D1&--level=0");
            }

            //mgstage.com 加入年龄认证Cookies
            if (request.RequestUri.ToString().Contains("mgstage.com") && !(request.Headers.TryGetValues("Cookie", out var cookies) && cookies.Contains("abc=1")))
                request.Headers.Add("Cookie", "adc=1");

            // Add UserAgent
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.130 Safari/537.36");

            var resp = await base.SendAsync(request, cancellationToken);

            //不需要重定向
            if (string.Compare(resp.ReasonPhrase, "Redirection", true) != 0)
                return resp;

            var html = await resp.Content.ReadAsStringAsync();

            var m = regexUrl.Match(html);
            if (m.Success)
            {
                request.RequestUri = new Uri(m.Groups["url"].Value);
                return await SendAsync(request, cancellationToken);
            }

            return resp;
        }
    }
}