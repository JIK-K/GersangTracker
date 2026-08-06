using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GersangTracker.Services
{
    public class GersangAuthService
    {
        public async Task<string> GetGameStartTokenAsync(string userId, string password)
        {
            var cookieContainer = new CookieContainer();
            using var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer,
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
            };

            using var client = new HttpClient(handler);

            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36");

            // 거상 로그인 요청 (POST)
            var loginData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("returnUrl", "www.gersang.co.kr/main/index.gs?"),
                new KeyValuePair<string, string>("GSuserID", userId),
                new KeyValuePair<string, string>("GSuserPW", password)
            });

            var loginResponse = await client.PostAsync("https://www.gersang.co.kr/member/loginProc.gs", loginData);
            loginResponse.EnsureSuccessStatusCode();

            // 메인 페이지 요청 (GET)
            var mainResponse = await client.GetAsync("https://www.gersang.co.kr/main/index.gs");
            mainResponse.EnsureSuccessStatusCode();

            var html = await mainResponse.Content.ReadAsStringAsync();

            // 실행 토큰 파싱
            var match = Regex.Match(html, @"wlogin\.CmdStr\s*=\s*['""]([^'""]+)['""]");
            if (match.Success)
            {
                string cmdStr = match.Groups[1].Value;
                cmdStr = cmdStr.Replace("\\t", "\t");
                return cmdStr;
            }

            throw new InvalidOperationException("로그인에 실패했거나 게임 실행 토큰(CmdStr)을 찾을 수 없습니다.\n(아이디/비밀번호 오류이거나 거상 홈페이지 로그인 시 캡챠가 발생했을 수 있습니다.)");
        }
    }
}