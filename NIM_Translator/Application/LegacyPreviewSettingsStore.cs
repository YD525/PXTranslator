using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using PhoenixEngine;
using PhoenixEngine.Language;
using PhoenixEngine.Platform;
using PhoenixEngine.Translate;
using NIM.UIManage;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Adapts the existing engine and local configuration to staged Settings Center persistence.
    /// </summary>
    internal sealed class LegacyPreviewSettingsStore : IPreviewSettingsStore
    {
        /// <inheritdoc />
        public IReadOnlyList<PreviewProviderOption> GetProviders()
        {
            if (NIMApp.EngineSetting?.PlatformConfigs == null)
            {
                return new List<PreviewProviderOption>();
            }

            return NIMApp.EngineSetting.PlatformConfigs
                .OrderBy(pair => GetProviderName(pair.Key, pair.Value), StringComparer.CurrentCultureIgnoreCase)
                .Select(pair => new PreviewProviderOption(
                    pair.Key,
                    GetProviderName(pair.Key, pair.Value),
                    pair.Value.Platform == PlatformType.LMLocalAI ||
                    pair.Value.CustomInFo?.Type == CustomPlatformType.LocalAI,
                    pair.Value.ApiKeys?.Any(key => !string.IsNullOrWhiteSpace(key)) == true,
                    pair.Value.CustomInFo == null && pair.Value.Platform != PlatformType.HumanTranslation,
                    GetModels(pair.Value)))
                .ToList();
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetLanguages()
        {
            return Enum.GetNames(typeof(Languages))
                .Where(name => !string.Equals(name, Languages.Null.ToString(), StringComparison.Ordinal))
                .ToList();
        }

        /// <inheritdoc />
        public PreviewSettingsSnapshot Load()
        {
            if (NIMApp.EngineSetting == null)
            {
                throw new InvalidOperationException("Engine configuration is not initialized.");
            }

            List<PreviewProviderOption> providers = GetProviders().ToList();
            int providerKey = SelectProviderKey(providers);
            PlatformConfig provider = GetProvider(providerKey);
            return new PreviewSettingsSnapshot
            {
                ProviderKey = providerKey,
                ProviderModel = provider?.Model ?? string.Empty,
                ProviderEnabled = provider?.Enable == true,
                HasStoredCredential = provider?.ApiKeys?.Any(key => !string.IsNullOrWhiteSpace(key)) == true,
                LocalPortText = (provider?.LocalPort > 0 ? provider.LocalPort : 1234)
                    .ToString(CultureInfo.InvariantCulture),
                SourceLanguage = NIMApp.SelfSetting.SourceLanguage.ToString(),
                TargetLanguage = NIMApp.SelfSetting.TargetLanguage.ToString(),
                EnableLanguageDetection = NIMApp.SelfSetting.EnableLanguageDetect,
                EnableContext = NIMApp.EngineSetting.ContextEnable,
                ContextLimitText = NIMApp.EngineSetting.ContextLimit.ToString(CultureInfo.InvariantCulture),
                AdditionalPrompt = NIMApp.EngineSetting.UserCustomAIPrompt ?? string.Empty,
                PlaceholderPattern = NIMApp.SelfSetting.P_Placeholders ?? string.Empty,
                GamePath = NIMApp.SelfSetting.SkyrimPath ?? string.Empty,
                ShowAssembly = NIMApp.SelfSetting.ShowAssembly,
                GenerateCSharp = NIMApp.SelfSetting.GenCSharp,
                AutoUpdateDatabase = NIMApp.SelfSetting.AutoUpdateStringsFileToDatabase,
                EnableGlobalSearch = NIMApp.EngineSetting.EnableGlobalSearch,
                UiLanguage = NIMApp.SelfSetting.CurrentUILanguage.ToString(),
                Density = string.IsNullOrWhiteSpace(NIMApp.SelfSetting.UiDensity)
                    ? "Compact"
                    : NIMApp.SelfSetting.UiDensity,
                RightToLeft = NIMApp.SelfSetting.TextDisplay == TextLayout.RTL,
                ProxyUrl = NIMApp.EngineSetting.ProxyUrl ?? string.Empty,
                ProxyUserName = NIMApp.EngineSetting.ProxyUserName ?? string.Empty,
                HasStoredProxyPassword = !string.IsNullOrEmpty(NIMApp.EngineSetting.ProxyPassword),
                MaxThreadCountText = NIMApp.EngineSetting.MaxThreadCount.ToString(CultureInfo.InvariantCulture),
                ThrottleRatioText = NIMApp.EngineSetting.ThrottleRatio.ToString(CultureInfo.InvariantCulture),
                ThrottleDelayText = NIMApp.EngineSetting.ThrottleDelayMs.ToString(CultureInfo.InvariantCulture)
            };
        }

        /// <inheritdoc />
        public void Save(PreviewSettingsSnapshot settings, string providerCredential, string proxyPassword)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlatformConfig provider = GetProvider(settings.ProviderKey);
            if (provider != null)
            {
                provider.Model = settings.ProviderModel;
                provider.Enable = settings.ProviderEnabled;
                int localPort;
                if (int.TryParse(settings.LocalPortText, NumberStyles.None, CultureInfo.InvariantCulture, out localPort))
                {
                    provider.LocalPort = localPort;
                }

                if (!string.IsNullOrWhiteSpace(providerCredential) &&
                    !provider.ApiKeys.Contains(providerCredential))
                {
                    provider.ApiKeys.Add(providerCredential);
                    Phoenix.ReSetKeyData();
                }
            }

            Languages language;
            if (Enum.TryParse(settings.SourceLanguage, out language))
            {
                NIMApp.SelfSetting.SourceLanguage = language;
            }

            if (Enum.TryParse(settings.TargetLanguage, out language))
            {
                NIMApp.SelfSetting.TargetLanguage = language;
            }

            if (Enum.TryParse(settings.UiLanguage, out language))
            {
                NIMApp.SelfSetting.CurrentUILanguage = language;
            }

            NIMApp.SelfSetting.EnableLanguageDetect = settings.EnableLanguageDetection;
            NIMApp.SelfSetting.P_Placeholders = settings.PlaceholderPattern;
            NIMApp.SelfSetting.SkyrimPath = settings.GamePath;
            NIMApp.SelfSetting.ShowAssembly = settings.ShowAssembly;
            NIMApp.SelfSetting.GenCSharp = settings.GenerateCSharp;
            NIMApp.SelfSetting.AutoUpdateStringsFileToDatabase = settings.AutoUpdateDatabase;
            NIMApp.SelfSetting.UiDensity = settings.Density;
            NIMApp.SelfSetting.TextDisplay = settings.RightToLeft ? TextLayout.RTL : TextLayout.LTR;

            NIMApp.EngineSetting.ContextEnable = settings.EnableContext;
            NIMApp.EngineSetting.ContextLimit = int.Parse(settings.ContextLimitText, CultureInfo.InvariantCulture);
            NIMApp.EngineSetting.UserCustomAIPrompt = settings.AdditionalPrompt.Trim();
            NIMApp.EngineSetting.EnableGlobalSearch = settings.EnableGlobalSearch;
            NIMApp.EngineSetting.ProxyUrl = settings.ProxyUrl.Trim();
            NIMApp.EngineSetting.ProxyUserName = settings.ProxyUserName.Trim();
            if (!string.IsNullOrEmpty(proxyPassword))
            {
                NIMApp.EngineSetting.ProxyPassword = proxyPassword;
            }

            NIMApp.EngineSetting.MaxThreadCount = int.Parse(settings.MaxThreadCountText, CultureInfo.InvariantCulture);
            NIMApp.EngineSetting.ThrottleRatio = double.Parse(settings.ThrottleRatioText, CultureInfo.InvariantCulture);
            NIMApp.EngineSetting.ThrottleDelayMs = int.Parse(settings.ThrottleDelayText, CultureInfo.InvariantCulture);
            NIMApp.SelfSetting.SaveConfig();
        }

        /// <inheritdoc />
        public async Task<PreviewProviderTestResult> TestProviderAsync(
            PreviewSettingsSnapshot settings,
            string providerCredential,
            string proxyPassword,
            CancellationToken cancellationToken)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PlatformConfig provider = GetProvider(settings.ProviderKey);
            if (provider == null || provider.CustomInFo != null ||
                provider.Platform == PlatformType.HumanTranslation)
            {
                return new PreviewProviderTestResult(PreviewProviderTestStatus.Unsupported);
            }

            string credential = string.IsNullOrWhiteSpace(providerCredential)
                ? provider.ApiKeys?.FirstOrDefault(key => !string.IsNullOrWhiteSpace(key)) ?? string.Empty
                : providerCredential;
            if (provider.Platform != PlatformType.LMLocalAI && string.IsNullOrWhiteSpace(credential))
            {
                return new PreviewProviderTestResult(PreviewProviderTestStatus.AuthenticationFailed);
            }

            HttpRequestMessage providerRequest;
            try
            {
                providerRequest = CreateProviderTestRequest(provider, settings, credential);
            }
            catch (FormatException)
            {
                return new PreviewProviderTestResult(PreviewProviderTestStatus.AuthenticationFailed);
            }

            using (HttpRequestMessage request = providerRequest)
            {
                if (request == null)
                {
                    return new PreviewProviderTestResult(PreviewProviderTestStatus.Unsupported);
                }

                try
                {
                    using (HttpClientHandler handler = CreateHttpHandler(
                        provider.Platform == PlatformType.LMLocalAI,
                        settings,
                        proxyPassword))
                    using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) })
                    using (HttpResponseMessage response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            return new PreviewProviderTestResult(PreviewProviderTestStatus.Succeeded);
                        }

                        if (response.StatusCode == HttpStatusCode.Unauthorized ||
                            response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            return new PreviewProviderTestResult(PreviewProviderTestStatus.AuthenticationFailed);
                        }

                        return new PreviewProviderTestResult(PreviewProviderTestStatus.Rejected);
                    }
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return new PreviewProviderTestResult(PreviewProviderTestStatus.TimedOut);
                }
                catch (HttpRequestException)
                {
                    return new PreviewProviderTestResult(PreviewProviderTestStatus.Unreachable);
                }
                catch (WebException)
                {
                    return new PreviewProviderTestResult(PreviewProviderTestStatus.Unreachable);
                }
                catch (FormatException)
                {
                    return new PreviewProviderTestResult(PreviewProviderTestStatus.Rejected);
                }
            }
        }

        private static HttpRequestMessage CreateProviderTestRequest(
            PlatformConfig provider,
            PreviewSettingsSnapshot settings,
            string credential)
        {
            HttpRequestMessage request;
            switch (provider.Platform)
            {
                case PlatformType.ChatGpt:
                    request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
                    break;
                case PlatformType.DeepSeek:
                    request = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/models");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
                    break;
                case PlatformType.Gemini:
                    request = new HttpRequestMessage(
                        HttpMethod.Get,
                        "https://generativelanguage.googleapis.com/v1beta/models");
                    request.Headers.Add("x-goog-api-key", credential);
                    break;
                case PlatformType.DeepL:
                    request = new HttpRequestMessage(
                        HttpMethod.Get,
                        provider.IsFree
                            ? "https://api-free.deepl.com/v2/usage"
                            : "https://api.deepl.com/v2/usage");
                    request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", credential);
                    break;
                case PlatformType.LMLocalAI:
                    int port;
                    if (!int.TryParse(settings.LocalPortText, NumberStyles.None, CultureInfo.InvariantCulture, out port) ||
                        port < 1 || port > 65535)
                    {
                        return null;
                    }

                    request = new HttpRequestMessage(
                        HttpMethod.Get,
                        string.Format(CultureInfo.InvariantCulture, "http://localhost:{0}/v1/models", port));
                    break;
                default:
                    return null;
            }

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }

        private static HttpClientHandler CreateHttpHandler(
            bool isLocal,
            PreviewSettingsSnapshot settings,
            string stagedProxyPassword)
        {
            var handler = new HttpClientHandler();
            if (isLocal)
            {
                handler.UseProxy = false;
                return handler;
            }

            if (string.IsNullOrWhiteSpace(settings.ProxyUrl))
            {
                return handler;
            }

            var proxy = new WebProxy(new Uri(settings.ProxyUrl, UriKind.Absolute));
            string password = string.IsNullOrEmpty(stagedProxyPassword)
                ? NIMApp.EngineSetting?.ProxyPassword ?? string.Empty
                : stagedProxyPassword;
            if (!string.IsNullOrWhiteSpace(settings.ProxyUserName) || !string.IsNullOrEmpty(password))
            {
                proxy.Credentials = new NetworkCredential(settings.ProxyUserName, password);
            }

            handler.Proxy = proxy;
            handler.UseProxy = true;
            return handler;
        }

        private static int SelectProviderKey(IReadOnlyList<PreviewProviderOption> providers)
        {
            if (providers.Count == 0)
            {
                return 0;
            }

            PreviewProviderOption enabled = providers.FirstOrDefault(option => GetProvider(option.Key)?.Enable == true);
            PreviewProviderOption chatGpt = providers.FirstOrDefault(option =>
                GetProvider(option.Key)?.Platform == PlatformType.ChatGpt);
            return (enabled ?? chatGpt ?? providers[0]).Key;
        }

        private static PlatformConfig GetProvider(int key)
        {
            if (NIMApp.EngineSetting?.PlatformConfigs == null)
            {
                return null;
            }

            PlatformConfig provider;
            return NIMApp.EngineSetting.PlatformConfigs.TryGetValue(key, out provider) ? provider : null;
        }

        private static string GetProviderName(int key, PlatformConfig provider)
        {
            if (!string.IsNullOrWhiteSpace(provider?.CustomInFo?.Name))
            {
                return provider.CustomInFo.Name;
            }

            switch (provider?.Platform)
            {
                case PlatformType.ChatGpt:
                    return "ChatGPT";
                case PlatformType.DeepSeek:
                    return "DeepSeek";
                case PlatformType.Gemini:
                    return "Gemini";
                case PlatformType.DeepL:
                    return "DeepL";
                case PlatformType.LMLocalAI:
                    return "LM Studio";
                case PlatformType.HumanTranslation:
                    return "Interactive translation";
                default:
                    return string.Format(CultureInfo.InvariantCulture, "Provider {0}", key);
            }
        }

        private static IReadOnlyList<string> GetModels(PlatformConfig provider)
        {
            var models = new List<string>();
            if (!string.IsNullOrWhiteSpace(provider?.Model))
            {
                models.Add(provider.Model);
            }

            if (provider?.CustomInFo != null)
            {
                return models;
            }

            switch (provider?.Platform)
            {
                case PlatformType.ChatGpt:
                    AddDistinct(models, "gpt-5-nano", "gpt-5-mini", "gpt-4.1-nano", "gpt-4.1-mini", "gpt-4o-mini");
                    break;
                case PlatformType.Gemini:
                    AddDistinct(models, "gemini-2.5-flash", "gemini-2.0-flash");
                    break;
                case PlatformType.DeepSeek:
                    AddDistinct(models, "deepseek-v4-pro");
                    break;
            }

            return models;
        }

        private static void AddDistinct(ICollection<string> target, params string[] values)
        {
            foreach (string value in values)
            {
                if (!target.Contains(value))
                {
                    target.Add(value);
                }
            }
        }
    }
}
