using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using NIMEngine;
using NIMEngine.ADO;
using NIMEngine.Platform;
using NIMEngine.Translate;

namespace NIM.ApplicationLayer
{
    /// <summary>Adapts legacy engine configuration to the guarded Advanced Tools boundary.</summary>
    internal sealed class LegacyPreviewAdvancedToolsStore : IPreviewAdvancedToolsStore
    {
        private static readonly HttpClient ProviderProbeClient = CreateProviderProbeClient();

        /// <inheritdoc />
        public IReadOnlyList<PreviewPipelineEntry> LoadPipeline()
        {
            if (NIMApp.EngineSetting?.PlatformConfigs == null)
            {
                return new List<PreviewPipelineEntry>();
            }

            return NIMApp.EngineSetting.PlatformConfigs.Select(pair => new PreviewPipelineEntry(
                pair.Key,
                GetName(pair.Key, pair.Value),
                GetGroup(pair.Value),
                pair.Value.Enable,
                pair.Value.CustomInFo != null)).ToList();
        }

        /// <inheritdoc />
        public void SavePipeline(IReadOnlyList<PreviewPipelineEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            Dictionary<int, PlatformConfig> current = NIMApp.EngineSetting.PlatformConfigs;
            if (current == null || entries.Count != current.Count || entries.Any(entry => !current.ContainsKey(entry.Key)))
            {
                throw new InvalidOperationException("The provider pipeline changed while edits were staged.");
            }

            var ordered = new Dictionary<int, PlatformConfig>();
            foreach (PreviewPipelineEntry entry in entries)
            {
                PlatformConfig config = current[entry.Key];
                config.Enable = entry.IsEnabled;
                ordered.Add(entry.Key, config);
            }
            NIMApp.EngineSetting.PlatformConfigs = ordered;
            NIM_Engine.SaveConfig();
        }

        /// <inheritdoc />
        public async Task<PreviewProviderTestResult> TestCustomProviderAsync(
            PreviewCustomProviderDraft draft,
            CancellationToken cancellationToken)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            Uri endpoint;
            if (!Uri.TryCreate(draft.Endpoint, UriKind.Absolute, out endpoint))
            {
                return new PreviewProviderTestResult(PreviewProviderTestStatus.Rejected);
            }
            if (endpoint.Scheme == Uri.UriSchemeHttp && !IPAddress.IsLoopback(ResolveAddress(endpoint.Host)))
            {
                return new PreviewProviderTestResult(PreviewProviderTestStatus.Rejected);
            }

            using (var request = new HttpRequestMessage(HttpMethod.Head, endpoint))
            using (HttpResponseMessage response = await ProviderProbeClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.MethodNotAllowed
                    ? new PreviewProviderTestResult(PreviewProviderTestStatus.Succeeded)
                    : new PreviewProviderTestResult(PreviewProviderTestStatus.Rejected);
            }
        }

        /// <inheritdoc />
        public void SaveCustomProvider(PreviewCustomProviderDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            int key = NIMApp.EngineSetting.PlatformConfigs.Count == 0 ? 1 : NIMApp.EngineSetting.PlatformConfigs.Keys.Max() + 1;
            while (NIMApp.EngineSetting.PlatformConfigs.ContainsKey(key)) key++;
            var custom = new CustomPlatformInFo
            {
                CustomID = key,
                Name = draft.Name.Trim(),
                Url = draft.Endpoint.Trim(),
                IsPost = draft.UsePost,
                Type = ParseGroup(draft.Group),
                QueryRule = new ReqQueryRuleItem { ByJson = true, FieldName = draft.ResponseField.Trim() }
            };
            var request = new CustomReqCore();
            request.SetUrl(custom.Url);
            request.SetHeader(draft.Headers ?? string.Empty);
            request.SetPayLoad(draft.Payload ?? string.Empty);
            custom.Header = draft.Headers ?? string.Empty;
            custom.PayLoad = draft.Payload ?? string.Empty;
            custom.Url_Tags = CreateTags(request.GetUrlKeyValues());
            custom.Header_Tags = CreateTags(request.GetHeaderKeyValues());
            custom.PayLoad_Tags = CreateTags(request.GetPayLoadKeyValues());
            NIMApp.EngineSetting.PlatformConfigs.Add(key, new PlatformConfig(PlatformType.CustomPlatform)
            {
                Enable = false,
                Model = draft.Model?.Trim() ?? string.Empty,
                CustomInFo = custom
            });
            NIM_Engine.SaveConfig();
        }

        /// <inheritdoc />
        public IReadOnlyList<PreviewDatabaseResultRow> ExecuteDatabaseQuery(string sql, bool allowMutation)
        {
            string statement = (sql ?? string.Empty).Trim();
            if (statement.EndsWith(";", StringComparison.Ordinal))
            {
                statement = statement.Substring(0, statement.Length - 1).TrimEnd();
            }

            if (string.IsNullOrWhiteSpace(statement) || statement.IndexOf(';') >= 0 ||
                (!allowMutation && !PreviewDatabaseStatementGuard.IsReadOnly(statement)))
            {
                throw new InvalidOperationException("The database statement is not allowed in the current mode.");
            }

            string query = PreviewDatabaseStatementGuard.IsReadOnly(statement)
                ? "SELECT * FROM (" + statement + ") AS PreviewResult LIMIT 1000"
                : statement;
            List<Dictionary<string, object>> rows = NIM_Engine.LocalDB.P_ExecuteQuery(
                SQLSafeCodec.EncodeSQLValues(query)) ?? new List<Dictionary<string, object>>();
            return rows.Take(1000).Select(FormatDatabaseRow).ToList();
        }

        /// <inheritdoc />
        public IReadOnlyList<KeyValuePair<string, long>> ReadTokenUsage()
        {
            return new[]
            {
                Pair("ChatGPT", NIMApp.SelfSetting.ChatGPTTokenUsage),
                Pair("Gemini", NIMApp.SelfSetting.GeminiTokenUsage),
                Pair("Cohere", NIMApp.SelfSetting.CohereTokenUsage),
                Pair("DeepSeek", NIMApp.SelfSetting.DeepSeekTokenUsage),
                Pair("Local AI", NIMApp.SelfSetting.LocalAITokenUsage)
            };
        }

        /// <inheritdoc />
        public void ClearTokenUsage()
        {
            NIMApp.SelfSetting.ChatGPTTokenUsage = 0;
            NIMApp.SelfSetting.GeminiTokenUsage = 0;
            NIMApp.SelfSetting.CohereTokenUsage = 0;
            NIMApp.SelfSetting.DeepSeekTokenUsage = 0;
            NIMApp.SelfSetting.LocalAITokenUsage = 0;
            NIMApp.SelfSetting.SaveConfig();
        }

        private static HttpClient CreateProviderProbeClient()
        {
            return new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(8),
                MaxResponseContentBufferSize = 1024
            };
        }

        private static PreviewDatabaseResultRow FormatDatabaseRow(Dictionary<string, object> row)
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, object> column in row)
            {
                if (text.Length > 0)
                {
                    text.Append("  |  ");
                }

                string value = column.Value == null ? "(null)" : column.Value.ToString();
                if (string.Equals(column.Key, "Source", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(column.Key, "Result", StringComparison.OrdinalIgnoreCase))
                {
                    value = SQLSafeCodec.Decode(value);
                }

                if (value.Length > 4096)
                {
                    value = value.Substring(0, 4096) + "…";
                }
                text.Append(column.Key).Append(": ").Append(value);
            }
            return new PreviewDatabaseResultRow(text.ToString());
        }

        private static IPAddress ResolveAddress(string host)
        {
            IPAddress parsed;
            if (IPAddress.TryParse(host, out parsed)) return parsed;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return IPAddress.Loopback;
            return IPAddress.None;
        }

        private static KeyValuePair<string, long> Pair(string name, int value)
        {
            return new KeyValuePair<string, long>(name, Math.Max(0, value));
        }

        private static List<ReqReplaceTag> CreateTags(IEnumerable<ReqCustomKeyValue> values)
        {
            return values.Select(value => new ReqReplaceTag(value.Key, value.Value)).ToList();
        }

        private static string GetName(int key, PlatformConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.CustomInFo?.Name)) return config.CustomInFo.Name;
            return config.Platform == PlatformType.LMLocalAI ? "LM Studio" : config.Platform.ToString();
        }

        private static string GetGroup(PlatformConfig config)
        {
            if (config.CustomInFo != null) return FormatGroup(config.CustomInFo.Type);
            switch (config.Platform)
            {
                case PlatformType.ChatGpt:
                case PlatformType.Gemini:
                case PlatformType.DeepSeek: return "Cloud AI";
                case PlatformType.LMLocalAI: return "Local AI";
                case PlatformType.HumanTranslation: return "Interactive";
                default: return "Traditional";
            }
        }

        private static string FormatGroup(CustomPlatformType type)
        {
            switch (type)
            {
                case CustomPlatformType.CloudAI: return "Cloud AI";
                case CustomPlatformType.LocalAI: return "Local AI";
                case CustomPlatformType.Traditional: return "Traditional";
                default: return "Interactive";
            }
        }

        private static CustomPlatformType ParseGroup(string group)
        {
            if (string.Equals(group, "Local AI", StringComparison.Ordinal)) return CustomPlatformType.LocalAI;
            if (string.Equals(group, "Traditional", StringComparison.Ordinal)) return CustomPlatformType.Traditional;
            return CustomPlatformType.CloudAI;
        }
    }
}
