using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NIM.ApplicationLayer;
using NIM.Properties;
using NIM.UIManagement.Preview;

namespace NIM.PresetTests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args != null && args.Any(argument =>
                string.Equals(argument, "--performance", StringComparison.OrdinalIgnoreCase)))
            {
                return RunPerformanceValidation();
            }

            var tests = new Dictionary<string, Action>
            {
                { nameof(PreservesCustomSettingsOnLoad), PreservesCustomSettingsOnLoad },
                { nameof(AppliesNamedPresetOnce), AppliesNamedPresetOnce },
                { nameof(MarksManualEditsAsCustom), MarksManualEditsAsCustom },
                { nameof(BoundsAndValidatesPresetValues), BoundsAndValidatesPresetValues },
                { nameof(BuildsProjectIndependentDatabaseQuery), BuildsProjectIndependentDatabaseQuery },
                { nameof(BuildsLanguageFilteredDatabaseQuery), BuildsLanguageFilteredDatabaseQuery },
                { nameof(ValidatesReferenceCorpusManifest), ValidatesReferenceCorpusManifest },
                { nameof(ValidatesMcmFixtures), ValidatesMcmFixtures },
                { nameof(ValidatesXmlFixtures), ValidatesXmlFixtures },
                { nameof(ValidatesPreviewMessageIdentifiers), ValidatesPreviewMessageIdentifiers },
                { nameof(FormatsPreviewMessages), FormatsPreviewMessages },
                { nameof(NavigatesPreviewShell), NavigatesPreviewShell },
                { nameof(TracksPreviewShellStatus), TracksPreviewShellStatus },
                { nameof(OpensAndClosesPreviewShellServices), OpensAndClosesPreviewShellServices },
                { nameof(RedactsAndBoundsPreviewDiagnostics), RedactsAndBoundsPreviewDiagnostics },
                { nameof(ExportsSafePreviewDiagnostics), ExportsSafePreviewDiagnostics },
                { nameof(PresentsPreviewShellServiceVersions), PresentsPreviewShellServiceVersions },
                { nameof(OpensProjectThroughHubAndPersistsRecent), OpensProjectThroughHubAndPersistsRecent },
                { nameof(ProtectsUnsavedProjectReplacement), ProtectsUnsavedProjectReplacement },
                { nameof(PersistsBoundedRecentProjects), PersistsBoundedRecentProjects },
                { nameof(FiltersPreviewTranslationWorkspace), FiltersPreviewTranslationWorkspace },
                { nameof(HandlesPreviewTranslationFailures), HandlesPreviewTranslationFailures },
                { nameof(CancelsPreviewTranslationWork), CancelsPreviewTranslationWork },
                { nameof(FiltersLargePreviewTranslationProject), FiltersLargePreviewTranslationProject },
                { nameof(CancelsLargeQualityValidation), CancelsLargeQualityValidation },
                { nameof(CancelsLargeRevisionComparison), CancelsLargeRevisionComparison },
                { nameof(LoadsAndNavigatesPreviewContext), LoadsAndNavigatesPreviewContext },
                { nameof(DiscardsStalePreviewContext), DiscardsStalePreviewContext },
                { nameof(DistinguishesEmptyAndFailedPreviewContext), DistinguishesEmptyAndFailedPreviewContext },
                { nameof(ValidatesBoundedPreviewAssets), ValidatesBoundedPreviewAssets },
                { nameof(PreviewsAppliesAndUndoesWorkspaceReplacement), PreviewsAppliesAndUndoesWorkspaceReplacement },
                { nameof(RoundTripsBoundedTranslationTable), RoundTripsBoundedTranslationTable },
                { nameof(RoundTripsWorkflowRolloutAndRecoversBackup), RoundTripsWorkflowRolloutAndRecoversBackup },
                { nameof(RoutesDisabledWorkflowToLegacyFallback), RoutesDisabledWorkflowToLegacyFallback },
                { nameof(RoundTripsBoundedRamCache), RoundTripsBoundedRamCache },
                { nameof(ClearsOnlyConfirmedCacheScope), ClearsOnlyConfirmedCacheScope },
                { nameof(ManagesTranslationHistoryThroughWorkspaceBoundary), ManagesTranslationHistoryThroughWorkspaceBoundary },
                { nameof(ValidatesInteractiveProviderRequestIdentity), ValidatesInteractiveProviderRequestIdentity },
                { nameof(StagesWritingVariantAndTracksExportReadiness), StagesWritingVariantAndTracksExportReadiness },
                { nameof(AnalyzesMixedQualityFindings), AnalyzesMixedQualityFindings },
                { nameof(TracksReviewDecisionsAndBulkUndo), TracksReviewDecisionsAndBulkUndo },
                { nameof(PersistsPrivateReviewMetadata), PersistsPrivateReviewMetadata },
                { nameof(NavigatesFromFindingToTranslationEntry), NavigatesFromFindingToTranslationEntry },
                { nameof(ClassifiesProjectRevisionChanges), ClassifiesProjectRevisionChanges },
                { nameof(PreservesReviewedTargetsAsConflicts), PreservesReviewedTargetsAsConflicts },
                { nameof(PersistsPrivateRevisionHistory), PersistsPrivateRevisionHistory },
                { nameof(AppliesAndUndoesExplicitRevisionReuse), AppliesAndUndoesExplicitRevisionReuse },
                { nameof(StagesAndCancelsUnifiedSettings), StagesAndCancelsUnifiedSettings },
                { nameof(ValidatesAndAppliesUnifiedSettings), ValidatesAndAppliesUnifiedSettings },
                { nameof(SearchesSettingsByLegacyTerminology), SearchesSettingsByLegacyTerminology },
                { nameof(ProtectsSettingsSecrets), ProtectsSettingsSecrets },
                { nameof(TestsProviderWithoutSavingSettings), TestsProviderWithoutSavingSettings },
                { nameof(ClearsStagedCredentialWhenProviderChanges), ClearsStagedCredentialWhenProviderChanges },
                { nameof(StagesAndAppliesProviderPipeline), StagesAndAppliesProviderPipeline },
                { nameof(ValidatesCustomProviderDrafts), ValidatesCustomProviderDrafts },
                { nameof(CancelsCustomProviderConnectivityTest), CancelsCustomProviderConnectivityTest },
                { nameof(GuardsReadOnlyDatabaseStatements), GuardsReadOnlyDatabaseStatements },
                { nameof(ExecutesDatabaseStatementsInsideAdvancedWorkspace), ExecutesDatabaseStatementsInsideAdvancedWorkspace },
                { nameof(ValidatesSemanticIconRegistry), ValidatesSemanticIconRegistry }
            };
            int failures = 0;
            foreach (KeyValuePair<string, Action> test in tests)
            {
                try
                {
                    test.Value();
                    Console.WriteLine("Passed {0}", test.Key);
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine("Failed {0}: {1}", test.Key, exception);
                }
            }

            Console.WriteLine("{0} tests passed; {1} failed.", tests.Count - failures, failures);
            return failures == 0 ? 0 : 1;
        }

        private static void PreservesCustomSettingsOnLoad()
        {
            var store = new RecordingStore(
                TranslationPreset.Custom,
                new TranslationPresetSettings(777, 4321, true, true, false));
            var coordinator = CreateCoordinator(store);

            TranslationPresetSelection selection = coordinator.Load();

            AssertEqual(TranslationPreset.Custom, selection.Preset, "Existing values must remain custom.");
            AssertEqual(777, store.Settings.ContextLimit, "Loading must preserve the context limit.");
            AssertEqual(0, store.SaveCalls, "Loading must not save configuration.");
        }

        private static void AppliesNamedPresetOnce()
        {
            var store = new RecordingStore(
                TranslationPreset.Custom,
                new TranslationPresetSettings(777, 4321, true, true, false));
            var coordinator = CreateCoordinator(store);
            TranslationPresetSelection selection;

            AssertEqual(true, coordinator.TrySelect(TranslationPreset.Balanced, out selection),
                "Balanced must be selectable.");
            AssertEqual(200, store.Settings.ContextLimit, "Balanced must apply the complete context limit.");
            AssertEqual(3900, store.Settings.BucketLengthLimit, "Balanced must apply the complete bucket limit.");
            AssertEqual(1, store.SaveCalls, "One selection must save exactly once.");
        }

        private static void MarksManualEditsAsCustom()
        {
            var store = new RecordingStore(
                TranslationPreset.Balanced,
                new TranslationPresetSettings(200, 3900, false, false, false));
            var coordinator = CreateCoordinator(store);
            TranslationPresetSelection selection;

            AssertEqual(true, coordinator.TryApplyCustomSettings(
                new TranslationPresetSettings(250, 3900, false, false, false),
                false,
                out selection),
                "A valid manual edit must be applied.");
            AssertEqual(TranslationPreset.Custom, store.Preset, "A manual edit must select Custom.");
            AssertEqual(0, store.SaveCalls, "A text edit may use the existing shutdown save.");
        }

        private static void BoundsAndValidatesPresetValues()
        {
            var service = new TranslationPresetService();
            foreach (TranslationPreset preset in new[]
            {
                TranslationPreset.Balanced,
                TranslationPreset.QualityFirst,
                TranslationPreset.SpeedFirst
            })
            {
                TranslationPresetProfile profile;
                AssertEqual(true, service.TryGetProfile(preset, out profile),
                    preset + " must define a complete profile.");
                AssertEqual(true, profile.TranslationQuality >= 0 && profile.TranslationQuality <= 100,
                    preset + " quality must stay within the radar range.");
                AssertEqual(true, profile.TranslationSpeed >= 0 && profile.TranslationSpeed <= 100,
                    preset + " speed must stay within the radar range.");
            }

            AssertEqual(false, service.IsValid(
                new TranslationPresetSettings(0, 3900, false, false, false)),
                "Zero context length must be rejected.");
        }

        private static void BuildsProjectIndependentDatabaseQuery()
        {
            AssertEqual(
                "Select * From AdvancedDictionary Limit 100000",
                AdvancedDictionaryQueryBuilder.Build(null, null),
                "Opening the database without a project must use a bounded query.");
        }

        private static void BuildsLanguageFilteredDatabaseQuery()
        {
            AssertEqual(
                "Select * From AdvancedDictionary Where [From] = 1 And [To] = 2 Limit 100000",
                AdvancedDictionaryQueryBuilder.Build(1, 2),
                "Opening the database with a project must retain its language filter.");
        }

        private static void ValidatesReferenceCorpusManifest()
        {
            string testDataDirectory = GetTestDataDirectory();
            string[] rows = File.ReadAllLines(Path.Combine(testDataDirectory, "manifest.tsv"));
            AssertEqual(5, rows.Length, "The fixture manifest must contain four records and one header.");

            foreach (string row in rows.Skip(1))
            {
                string[] columns = row.Split('\t');
                AssertEqual(4, columns.Length, "Every fixture manifest row must contain four columns.");
                string fixturePath = Path.Combine(
                    testDataDirectory,
                    columns[0].Replace('/', Path.DirectorySeparatorChar));
                AssertEqual(true, File.Exists(fixturePath), "Every manifest entry must resolve to a fixture.");
                AssertEqual(columns[1], ComputeSha256(fixturePath), "Fixture hashes must remain reproducible.");
                AssertEqual("Project-owned", columns[2], "Synthetic fixtures must retain their ownership marker.");
                AssertEqual("Synthetic", columns[3], "External content must not enter the synthetic corpus.");
            }
        }

        private static void ValidatesMcmFixtures()
        {
            string testDataDirectory = GetTestDataDirectory();
            string[] validLines = File.ReadAllLines(Path.Combine(testDataDirectory, "Mcm", "valid-mcm-english.txt"));
            string[] malformedLines = File.ReadAllLines(Path.Combine(testDataDirectory, "Mcm", "malformed-mcm.txt"));

            AssertEqual(true, LooksLikeMcm(validLines), "The valid MCM fixture must retain its expected structure.");
            AssertEqual(false, LooksLikeMcm(malformedLines), "The malformed MCM fixture must remain invalid.");
            AssertEqual(true, validLines[1].Contains("\\n"), "The valid fixture must cover escaped line breaks.");
        }

        private static void ValidatesXmlFixtures()
        {
            string testDataDirectory = GetTestDataDirectory();
            XDocument validDocument = XDocument.Load(Path.Combine(testDataDirectory, "Xml", "valid-translation.xml"));
            AssertEqual(2, validDocument.Descendants("String").Count(),
                "The valid XML fixture must contain two translation records.");
            XDocument reviewDocument = XDocument.Load(
                Path.Combine(testDataDirectory, "Xml", "mixed-review-quality.xml"));
            AssertEqual(5, reviewDocument.Descendants("String").Count(),
                "The mixed review fixture must retain every deterministic finding scenario.");

            bool rejected = false;
            try
            {
                XDocument.Load(Path.Combine(testDataDirectory, "Xml", "malformed-translation.xml"));
            }
            catch (XmlException)
            {
                rejected = true;
            }

            AssertEqual(true, rejected, "The malformed XML fixture must fail deterministic parsing.");
        }

        private static string GetTestDataDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static bool LooksLikeMcm(IEnumerable<string> lines)
        {
            int remainingCandidates = 2;
            foreach (string line in lines)
            {
                string value = line.TrimStart('\uFEFF').Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                remainingCandidates--;
                if ((value.StartsWith("$") || value.StartsWith("#")) &&
                    (value.Contains("\t") || value.Contains(" ")))
                {
                    return true;
                }

                if (remainingCandidates == 0)
                {
                    break;
                }
            }

            return false;
        }

        private static void ValidatesPreviewMessageIdentifiers()
        {
            var entries = new Dictionary<string, string>();
            ResourceSet resourceSet = Resources.ResourceManager.GetResourceSet(
                CultureInfo.InvariantCulture,
                true,
                true);
            foreach (DictionaryEntry entry in resourceSet)
            {
                entries.Add((string)entry.Key, (string)entry.Value);
            }

            AssertEqual(true, entries.Count >= 70, "The preview source catalogue must retain its baseline coverage.");
            foreach (KeyValuePair<string, string> entry in entries)
            {
                AssertEqual(true,
                    Regex.IsMatch(entry.Key, "^[A-Z][A-Za-z0-9]*(?:_[A-Z][A-Za-z0-9]*)+$"),
                    entry.Key + " must use stable PascalCase segments separated by underscores.");
                AssertEqual(false, string.IsNullOrWhiteSpace(entry.Value), entry.Key + " must have English source text.");

                MatchCollection placeholders = Regex.Matches(entry.Value, "\\{(\\d+)(?:[^}]*)\\}");
                if (placeholders.Count > 0)
                {
                    int[] indexes = placeholders.Cast<Match>()
                        .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
                        .Distinct()
                        .OrderBy(index => index)
                        .ToArray();
                    AssertEqual(
                        string.Join(",", Enumerable.Range(0, indexes[indexes.Length - 1] + 1)),
                        string.Join(",", indexes),
                        entry.Key + " placeholders must be contiguous from zero.");
                }
            }

            foreach (string prefix in new[]
            {
                "Common_", "Shell_", "Workspace_", "Review_", "Quality_", "Settings_", "Accessibility_"
            })
            {
                AssertEqual(true, entries.Keys.Any(id => id.StartsWith(prefix, StringComparison.Ordinal)),
                    "The source catalogue must cover " + prefix.TrimEnd('_') + ".");
            }
        }

        private static void FormatsPreviewMessages()
        {
            AssertEqual(
                "Opening project: 42%",
                PreviewMessageCatalog.Format("Shell_ProjectOpen_OpeningProgress", 42),
                "Project progress must format with the current UI culture.");
            AssertEqual(
                "Translating 3 of 10 entries",
                PreviewMessageCatalog.Format("Workspace_Operation_TranslatingProgress", 3, 10),
                "Workspace progress must preserve both documented placeholders.");

            bool rejected = false;
            try
            {
                PreviewMessageCatalog.Get("Unknown_Message_Id");
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }

            AssertEqual(true, rejected, "Unknown preview message identifiers must fail explicitly.");
        }

        private static void NavigatesPreviewShell()
        {
            int legacyOpenCalls = 0;
            var viewModel = new PreviewShellViewModel(() => legacyOpenCalls++);

            AssertEqual(PreviewShellDestination.Projects, viewModel.CurrentDestination,
                "The preview shell must start at Projects.");
            AssertEqual("Projects", viewModel.CurrentTitle,
                "The initial destination must expose its localized title.");

            viewModel.NavigateCommand.Execute("Review");

            AssertEqual(PreviewShellDestination.Review, viewModel.CurrentDestination,
                "A keyboard command parameter must select the matching destination.");
            AssertEqual("Review", viewModel.CurrentTitle,
                "Navigation must update the localized destination title.");

            viewModel.OpenLegacyWorkspaceCommand.Execute(null);
            AssertEqual(1, legacyOpenCalls,
                "The fallback command must delegate to the legacy workspace integration exactly once.");
        }

        private static void TracksPreviewShellStatus()
        {
            var viewModel = new PreviewShellViewModel(() => { });

            AssertEqual("No project open", viewModel.ProjectIdentity,
                "The shell must expose an explicit no-project state.");
            AssertEqual("Ready", viewModel.StatusText,
                "The shell must start with an explicit idle status.");

            viewModel.SetProject("Example.esp", true);
            viewModel.SetWarningCount(3);
            viewModel.SetOperation("Shell_ProjectOpen_OpeningProgress", 42, 42);

            AssertEqual("Example.esp", viewModel.ProjectIdentity,
                "The shell must retain the safe project display name.");
            AssertEqual(true, viewModel.IsModified,
                "The shell must retain the unsaved project state.");
            AssertEqual("3 warnings", viewModel.WarningText,
                "The shell must format the persistent warning count.");
            AssertEqual("Opening project: 42%", viewModel.StatusText,
                "An active operation must replace the idle status.");
            AssertEqual(42d, viewModel.OperationProgress,
                "The shell must retain bounded operation progress.");

            viewModel.ShowNotification(
                PreviewShellNotificationSeverity.Error,
                "Shell_ProjectOpen_Failed");
            AssertEqual(true, viewModel.HasNotification,
                "The shell must expose a persistent error boundary.");
            AssertEqual(PreviewShellNotificationSeverity.Error, viewModel.NotificationSeverity,
                "The shell must retain notification severity independently from its text.");
            AssertEqual("Error", viewModel.NotificationSeverityText,
                "A notification must expose a non-color semantic severity label.");
            AssertEqual("The project could not be opened.", viewModel.NotificationMessage,
                "The error boundary must expose registered user-safe text.");
            viewModel.DismissNotificationCommand.Execute(null);
            AssertEqual(false, viewModel.HasNotification,
                "A non-blocking notification must be dismissible by command.");

            viewModel.CompleteOperation();
            AssertEqual("Ready", viewModel.StatusText,
                "Completing an operation must restore the idle status.");
            AssertEqual(0d, viewModel.OperationProgress,
                "Completing an operation must clear stale progress.");
        }

        private static void OpensAndClosesPreviewShellServices()
        {
            var viewModel = new PreviewShellViewModel(() => { });

            AssertEqual(false, viewModel.IsShellServicesVisible,
                "Cross-cutting shell services must not cover the initial workflow.");
            viewModel.OpenShellServicesCommand.Execute(null);
            AssertEqual(true, viewModel.IsShellServicesVisible,
                "The shell command must open About and diagnostics without changing workflow identity.");
            AssertEqual(PreviewShellDestination.Projects, viewModel.CurrentDestination,
                "Opening shell services must preserve the active workflow destination.");
            viewModel.CloseShellServicesCommand.Execute(null);
            AssertEqual(false, viewModel.IsShellServicesVisible,
                "The close command must return to the preserved workflow.");
        }

        private static void RedactsAndBoundsPreviewDiagnostics()
        {
            var diagnostics = new PreviewDiagnosticService();
            for (int index = 0; index < 205; index++)
            {
                diagnostics.Record(
                    PreviewDiagnosticSeverity.Warning,
                    "test.event." + index.ToString(CultureInfo.InvariantCulture),
                    "token=private-value C:\\Users\\Example\\private-project.xml");
            }

            IReadOnlyList<PreviewDiagnosticEntry> entries = diagnostics.GetEntries();
            AssertEqual(200, entries.Count,
                "Diagnostic retention must discard the oldest events beyond its documented bound.");
            AssertEqual("test.event.204", entries[0].EventId,
                "Diagnostic snapshots must expose the newest retained event first.");
            AssertEqual(false, entries.Any(entry => entry.Detail.Contains("private-value")),
                "Diagnostic retention must redact secret-like values.");
            AssertEqual(false, entries.Any(entry => entry.Detail.Contains("Example")),
                "Diagnostic retention must redact absolute machine-specific paths.");
        }

        private static void ExportsSafePreviewDiagnostics()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PhoenixDiagnosticTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "diagnostics.txt");
                var diagnostics = new PreviewDiagnosticService();
                diagnostics.Record(
                    PreviewDiagnosticSeverity.Error,
                    "test.export",
                    "password=hunter2 C:\\Private\\project.esp");
                diagnostics.Export(path);

                string report = File.ReadAllText(path);
                AssertEqual(true, report.Contains("test.export"),
                    "Diagnostic export must retain stable event identity.");
                AssertEqual(false, report.Contains("hunter2"),
                    "Diagnostic export must exclude secret-like values.");
                AssertEqual(false, report.Contains("C:\\Private"),
                    "Diagnostic export must exclude absolute paths.");
                AssertEqual(false, report.Contains(directory),
                    "Diagnostic export must not include its private destination.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void PresentsPreviewShellServiceVersions()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PhoenixShellServiceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "diagnostics.txt");
                var diagnostics = new PreviewDiagnosticService();
                diagnostics.Record(PreviewDiagnosticSeverity.Information, "test.ready");
                var dialogs = new RecordingPreviewDialogService();
                var viewModel = new PreviewShellServicesViewModel(diagnostics, () => path, dialogs);

                AssertEqual(5, viewModel.Components.Count,
                    "About must distinguish Translator from all four supporting component boundaries.");
                AssertEqual("Phoenix Translator", viewModel.Components[0].Component,
                    "About must present the product version independently from dependencies.");
                AssertEqual(true, viewModel.Components.All(component => !string.IsNullOrWhiteSpace(component.Status)),
                    "Every dependency must expose a safe loaded, available, or unavailable state.");

                viewModel.ExportDiagnosticsCommand.Execute(null);
                AssertEqual(true, File.Exists(path),
                    "The shell-services export command must create the selected report.");
                AssertEqual(1, dialogs.Requests.Count,
                    "The shell-services export command must report completion through the shared modal boundary.");
                AssertEqual(false, dialogs.Requests[0].RequiresConfirmation,
                    "A successful export notification must not masquerade as destructive confirmation.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void OpensProjectThroughHubAndPersistsRecent()
        {
            string projectPath = Path.Combine(Path.GetTempPath(), "PhoenixProjectHubTests", Guid.NewGuid().ToString("N"), "project.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
            File.WriteAllText(projectPath, "fixture");
            try
            {
                var project = new FakePreviewTranslationProject(
                    new[] { new PreviewTranslationEntry("1", "XML", "ENTRY", "Source", string.Empty, 100) },
                    projectPath);
                var shell = new PreviewShellViewModel(() => { });
                var workspace = new PreviewTranslationWorkspaceViewModel(
                    () => projectPath, () => { }, shell, path => project);
                var store = new RecordingRecentProjectStore();
                var hub = new PreviewProjectHubViewModel(
                    workspace, shell, () => projectPath, () => true, store, () => { });

                bool opened = hub.OpenProjectAsync(projectPath).GetAwaiter().GetResult();

                AssertEqual(true, opened,
                    "The Project Hub must report a successful normalized project open.");
                AssertEqual(PreviewShellDestination.Translate, shell.CurrentDestination,
                    "A successful project open must enter the translation workflow.");
                AssertEqual(1, hub.RecentProjects.Count,
                    "A successful project open must create one bounded recent reference.");
                AssertEqual("project.xml", hub.RecentProjects[0].DisplayName,
                    "Recent project UI metadata must expose only the safe file name.");
                AssertEqual(1, store.SaveCalls,
                    "A successful open must persist recent references exactly once.");
                hub.Dispose();
                workspace.Dispose();
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(projectPath), true);
            }
        }

        private static void ProtectsUnsavedProjectReplacement()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PhoenixProjectHubTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string firstPath = Path.Combine(directory, "first.xml");
            string secondPath = Path.Combine(directory, "second.xml");
            File.WriteAllText(firstPath, "first");
            File.WriteAllText(secondPath, "second");
            try
            {
                var firstProject = new FakePreviewTranslationProject(
                    new[] { new PreviewTranslationEntry("1", "XML", "ENTRY", "First", string.Empty, 100) },
                    firstPath);
                var secondProject = new FakePreviewTranslationProject(
                    new[] { new PreviewTranslationEntry("2", "XML", "ENTRY", "Second", string.Empty, 100) },
                    secondPath);
                var shell = new PreviewShellViewModel(() => { });
                var workspace = new PreviewTranslationWorkspaceViewModel(
                    () => firstPath,
                    () => { },
                    shell,
                    path => string.Equals(path, firstPath, StringComparison.Ordinal) ? firstProject : secondProject);
                var hub = new PreviewProjectHubViewModel(
                    workspace,
                    shell,
                    () => secondPath,
                    () => false,
                    new RecordingRecentProjectStore(),
                    () => { });

                AssertEqual(true, hub.OpenProjectAsync(firstPath).GetAwaiter().GetResult(),
                    "The initial project must open without an unsaved-state prompt.");
                workspace.SelectedEntry.TargetText = "Unsaved target";
                AssertEqual(false, hub.OpenProjectAsync(secondPath).GetAwaiter().GetResult(),
                    "Cancelling project replacement must reject the new project.");
                AssertEqual(firstPath, workspace.ProjectPath,
                    "Cancelling replacement must preserve the previous usable project.");
                AssertEqual("First", workspace.SelectedEntry.SourceText,
                    "Cancelling replacement must preserve normalized project content.");
                hub.Dispose();
                workspace.Dispose();
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void PersistsBoundedRecentProjects()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PhoenixProjectHubStoreTests", Guid.NewGuid().ToString("N"));
            string storePath = Path.Combine(directory, "recent.xml");
            Directory.CreateDirectory(directory);
            try
            {
                var store = new PreviewRecentProjectStore(storePath);
                var projects = Enumerable.Range(0, 15)
                    .Select(index => new PreviewRecentProject(
                        Path.Combine(directory, "project-" + index + ".xml"),
                        DateTime.UtcNow.AddMinutes(-index)))
                    .ToArray();

                store.Save(projects);
                IReadOnlyList<PreviewRecentProject> loaded = store.Load();

                AssertEqual(12, loaded.Count,
                    "Recent-project persistence must enforce its documented bound.");
                AssertEqual("project-0.xml", loaded[0].DisplayName,
                    "Recent projects must retain newest-first ordering.");
                AssertEqual(false, File.ReadAllText(storePath).Contains("Source"),
                    "Recent-project persistence must not contain translation content.");

                store.Save(loaded);
                AssertEqual(12, store.Load().Count,
                    "Atomic replacement must preserve the bounded recent-project list.");

                File.WriteAllText(storePath, "<!DOCTYPE recentProjects [<!ENTITY probe SYSTEM 'file:///missing'>]><recentProjects>&probe;</recentProjects>");
                AssertEqual(0, store.Load().Count,
                    "Recent-project parsing must reject DTD-backed content without resolving it.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void FiltersPreviewTranslationWorkspace()
        {
            var shell = new PreviewShellViewModel(() => { });
            var project = new FakePreviewTranslationProject(new[]
            {
                new PreviewTranslationEntry("1", "MCM", "GREETING", "Hello Dragonborn", "", 100),
                new PreviewTranslationEntry("2", "MCM", "FAREWELL", "Goodbye", "Auf Wiedersehen", 100),
                new PreviewTranslationEntry("3", "XML", "NOTICE", "Read this", "", 100)
            });
            var viewModel = new PreviewTranslationWorkspaceViewModel(
                () => "fixture",
                () => { },
                shell,
                path => project);

            viewModel.OpenProjectAsync("fixture").GetAwaiter().GetResult();

            AssertEqual(3, viewModel.Entries.Count,
                "Opening a project must expose every normalized entry.");
            AssertEqual("fixture.xml", shell.ProjectIdentity,
                "The shell must receive only the safe project display name.");
            AssertEqual(false, viewModel.SaveCommand.CanExecute(null),
                "An unchanged project must not offer a redundant save.");

            viewModel.SearchText = "dragonborn";
            AssertEqual(1, viewModel.Entries.Count,
                "Search must filter source text without changing project data.");

            viewModel.SearchText = string.Empty;
            viewModel.SelectedTypeFilter = "MCM";
            AssertEqual(2, viewModel.Entries.Count,
                "The type filter must retain matching normalized entries.");

            viewModel.SelectedEntry.TargetText = "Willkommen";
            AssertEqual(true, shell.IsModified,
                "Editing a target must update persistent shell unsaved state.");
            AssertEqual(true, viewModel.SaveCommand.CanExecute(null),
                "A staged target edit must enable persistence.");
            viewModel.Dispose();
        }

        private static void HandlesPreviewTranslationFailures()
        {
            var shell = new PreviewShellViewModel(() => { });
            var project = new FakePreviewTranslationProject(new[]
            {
                new PreviewTranslationEntry("1", "MCM", "ONE", "First", "", 100),
                new PreviewTranslationEntry("2", "MCM", "TWO", "Second", "", 100)
            })
            {
                FailedKey = "2"
            };
            var viewModel = new PreviewTranslationWorkspaceViewModel(
                () => "fixture",
                () => { },
                shell,
                path => project);
            viewModel.OpenProjectAsync("fixture").GetAwaiter().GetResult();

            viewModel.TranslateEntriesAsync(viewModel.Entries.ToList()).GetAwaiter().GetResult();

            AssertEqual("Translated First", project.Entries[0].TargetText,
                "A successful provider result must remain staged.");
            AssertEqual(string.Empty, project.Entries[1].TargetText,
                "A failed entry must preserve its previous target.");
            AssertEqual(PreviewShellNotificationSeverity.Warning, shell.NotificationSeverity,
                "Partial provider failure must use a warning rather than discard successful edits.");
            AssertEqual("Translation completed with 1 failed entries.", shell.NotificationMessage,
                "Partial failure must expose a user-safe bounded summary.");
            viewModel.Dispose();
        }

        private static void CancelsPreviewTranslationWork()
        {
            var shell = new PreviewShellViewModel(() => { });
            var project = new FakePreviewTranslationProject(new[]
            {
                new PreviewTranslationEntry("1", "MCM", "ONE", "First", "", 100)
            })
            {
                BlockUntilCancelled = true
            };
            var viewModel = new PreviewTranslationWorkspaceViewModel(
                () => "fixture",
                () => { },
                shell,
                path => project);
            viewModel.OpenProjectAsync("fixture").GetAwaiter().GetResult();

            System.Threading.Tasks.Task operation = viewModel.TranslateEntriesAsync(viewModel.Entries.ToList());
            AssertEqual(true, System.Threading.SpinWait.SpinUntil(() => project.TranslateStarted, 2000),
                "The synthetic provider must start before cancellation is requested.");
            viewModel.CancelOperationCommand.Execute(null);
            operation.GetAwaiter().GetResult();

            AssertEqual(false, viewModel.IsBusy,
                "Cooperative cancellation must return the workspace to its idle state.");
            AssertEqual(string.Empty, project.Entries[0].TargetText,
                "Cancellation must not replace an entry with a partial provider result.");
            AssertEqual("Ready", shell.StatusText,
                "Cooperative cancellation must restore persistent shell status.");
            viewModel.Dispose();
        }

        private static void FiltersLargePreviewTranslationProject()
        {
            var entries = Enumerable.Range(0, 25000)
                .Select(index => new PreviewTranslationEntry(
                    index.ToString(CultureInfo.InvariantCulture),
                    index % 2 == 0 ? "MCM" : "XML",
                    "RECORD_" + index.ToString(CultureInfo.InvariantCulture),
                    "Synthetic source " + index.ToString(CultureInfo.InvariantCulture),
                    index % 3 == 0 ? "Synthetic target" : string.Empty,
                    100))
                .ToList();
            var shell = new PreviewShellViewModel(() => { });
            var project = new FakePreviewTranslationProject(entries);
            var viewModel = new PreviewTranslationWorkspaceViewModel(
                () => "fixture",
                () => { },
                shell,
                path => project);

            viewModel.OpenProjectAsync("fixture").GetAwaiter().GetResult();
            AssertEqual(25000, viewModel.Entries.Count,
                "A large project must load as one complete virtualized list snapshot.");

            viewModel.SearchText = "source 24999";
            AssertEqual(1, viewModel.Entries.Count,
                "Search must isolate one record in a large synthetic project.");
            AssertEqual("24999", viewModel.Entries[0].Key,
                "Large-project filtering must retain stable record identity.");

            viewModel.SearchText = string.Empty;
            viewModel.SelectedTypeFilter = "MCM";
            AssertEqual(12500, viewModel.Entries.Count,
                "Large-project type filtering must retain every matching record.");
            viewModel.Dispose();
        }

        private static void CancelsLargeQualityValidation()
        {
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            bool cancelled = false;
            try
            {
                new PreviewQualityAnalyzer().Analyze(CreatePerformanceEntries(10000), cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            AssertEqual(true, cancelled,
                "Large-project quality validation must honor cooperative cancellation.");
        }

        private static void CancelsLargeRevisionComparison()
        {
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            bool cancelled = false;
            try
            {
                new PreviewProjectComparisonService().Compare(
                    CreatePerformanceEntries(10000),
                    CreatePerformanceEntries(10000),
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            AssertEqual(true, cancelled,
                "Large-project revision comparison must honor cooperative cancellation.");
        }

        private static int RunPerformanceValidation()
        {
            Console.WriteLine("Phoenix Translator preview performance validation");
            Console.WriteLine("Runtime,{0}", Environment.Version);
            Console.WriteLine("LogicalProcessors,{0}", Environment.ProcessorCount);
            Console.WriteLine("Profile,Entries,OpenMs,FilterMs,ValidationMs,ComparisonMs,Result");

            int failures = 0;
            failures += RunPerformanceProfile("small", 1000, 500, 100, 500, 500) ? 0 : 1;
            failures += RunPerformanceProfile("medium", 25000, 1500, 250, 2000, 1500) ? 0 : 1;
            failures += RunPerformanceProfile("large", 100000, 5000, 750, 7000, 5000) ? 0 : 1;
            failures += ValidateRepeatedLifecycle() ? 0 : 1;
            return failures == 0 ? 0 : 1;
        }

        private static bool RunPerformanceProfile(
            string name,
            int entryCount,
            long openBudget,
            long filterBudget,
            long validationBudget,
            long comparisonBudget)
        {
            IReadOnlyList<PreviewTranslationEntry> entries = CreatePerformanceEntries(entryCount);
            var project = new FakePreviewTranslationProject(entries);
            var shell = new PreviewShellViewModel(() => { });
            var workspace = new PreviewTranslationWorkspaceViewModel(
                () => project.Path,
                () => { },
                shell,
                path => project);

            long openMilliseconds = MeasureMilliseconds(
                () => workspace.OpenProjectAsync(project.Path).GetAwaiter().GetResult());
            long filterMilliseconds = MeasureMilliseconds(() => workspace.SearchText = "source " + (entryCount - 1));
            workspace.SearchText = string.Empty;
            long validationMilliseconds = MeasureMilliseconds(
                () => new PreviewQualityAnalyzer().Analyze(entries));
            long comparisonMilliseconds = MeasureMilliseconds(
                () => new PreviewProjectComparisonService().Compare(entries, entries));
            workspace.Dispose();

            bool passed = openMilliseconds <= openBudget && filterMilliseconds <= filterBudget &&
                validationMilliseconds <= validationBudget && comparisonMilliseconds <= comparisonBudget;
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6}",
                name,
                entryCount,
                openMilliseconds,
                filterMilliseconds,
                validationMilliseconds,
                comparisonMilliseconds,
                passed ? "PASS" : "FAIL"));
            return passed;
        }

        private static IReadOnlyList<PreviewTranslationEntry> CreatePerformanceEntries(int count)
        {
            return Enumerable.Range(0, count)
                .Select(index => new PreviewTranslationEntry(
                    index.ToString(CultureInfo.InvariantCulture),
                    index % 4 == 0 ? "MCM" : index % 4 == 1 ? "XML" : index % 4 == 2 ? "PEX" : "ESP",
                    "RECORD_" + index.ToString(CultureInfo.InvariantCulture),
                    "Synthetic source " + index.ToString(CultureInfo.InvariantCulture) + " {0}",
                    index % 5 == 0 ? string.Empty : "Synthetic target " + index.ToString(CultureInfo.InvariantCulture) + " {0}",
                    index % 7 == 0 ? 40 : 100))
                .ToList();
        }

        private static long MeasureMilliseconds(Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }

        private static bool ValidateRepeatedLifecycle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long managedBefore = GC.GetTotalMemory(true);
            using (Process process = Process.GetCurrentProcess())
            {
                process.Refresh();
                long privateBefore = process.PrivateMemorySize64;
                int handlesBefore = process.HandleCount;
                RunLifecycleCycles(5, 100000);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                process.Refresh();
                long managedDelta = Math.Max(0, GC.GetTotalMemory(true) - managedBefore);
                long privateDelta = Math.Max(0, process.PrivateMemorySize64 - privateBefore);
                int handleDelta = Math.Max(0, process.HandleCount - handlesBefore);
                bool passed = managedDelta <= 64L * 1024 * 1024 &&
                    privateDelta <= 64L * 1024 * 1024 && handleDelta <= 16;
                Console.WriteLine(
                    "Lifecycle,5x100000,ManagedDeltaBytes={0},PrivateDeltaBytes={1},HandleDelta={2},{3}",
                    managedDelta,
                    privateDelta,
                    handleDelta,
                    passed ? "PASS" : "FAIL");
                return passed;
            }
        }

        private static void RunLifecycleCycles(int cycles, int entryCount)
        {
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                IReadOnlyList<PreviewTranslationEntry> entries = CreatePerformanceEntries(entryCount);
                var project = new FakePreviewTranslationProject(entries);
                var shell = new PreviewShellViewModel(() => { });
                var workspace = new PreviewTranslationWorkspaceViewModel(
                    () => project.Path,
                    () => { },
                    shell,
                    path => project);
                workspace.OpenProjectAsync(project.Path).GetAwaiter().GetResult();
                project.Translate(entries[cycle % entries.Count], CancellationToken.None);
                new PreviewQualityAnalyzer().Analyze(entries);
                new PreviewProjectComparisonService().Compare(entries, entries);
                project.Export("synthetic-output", CancellationToken.None);
                workspace.Dispose();
            }
        }

        private static void LoadsAndNavigatesPreviewContext()
        {
            var first = CreateEntry("first", "needle", string.Empty);
            var second = CreateEntry("second", "Related source", string.Empty);
            string code = string.Join("\n", Enumerable.Repeat("needle call", 205));
            var context = new PreviewEntryContext(
                code,
                "PexInterface",
                new[] { new PreviewContextMetadata("Function", "OnInit", "PexInterface") },
                new[] { new PreviewContextRelation("second", "Related source", "INFO", "EspReader", "Dialogue") },
                new[] { new PreviewNpcContext("second", "Aela", "Female", "FemaleNord") },
                null);
            PreviewTranslationEntry navigatedEntry = null;
            var inspector = new PreviewContextInspectorViewModel(
                (entry, cancellationToken) => context,
                key => string.Equals(key, second.Key, StringComparison.Ordinal) ? second : null,
                entry => navigatedEntry = entry);

            inspector.SelectEntry(first);
            AssertEqual(true, SpinWait.SpinUntil(() => inspector.State == PreviewContextState.Ready, 2000),
                "A supported context snapshot must reach the ready state.");
            AssertEqual(true, SpinWait.SpinUntil(() => inspector.CodeResults.Count == 200, 2000),
                "Automatic code search must finish with its documented result bound.");
            AssertEqual(1, inspector.CodeResults[0].LineNumber,
                "Code search must retain exact one-based source line identity.");

            inspector.SelectedRelation = context.Relations[0];
            inspector.NavigateRelationCommand.Execute(null);
            AssertEqual(second, navigatedEntry,
                "Relationship navigation must resolve the stable normalized entry key.");

            navigatedEntry = null;
            inspector.SelectedNpc = context.Npcs[0];
            inspector.NavigateNpcCommand.Execute(null);
            AssertEqual(second, navigatedEntry,
                "NPC navigation must resolve the stable normalized entry key.");
            inspector.Dispose();
        }

        private static void DiscardsStalePreviewContext()
        {
            var first = CreateEntry("first", "First", string.Empty);
            var second = CreateEntry("second", "Second", string.Empty);
            bool firstStarted = false;
            var inspector = new PreviewContextInspectorViewModel(
                (entry, cancellationToken) =>
                {
                    if (ReferenceEquals(entry, first))
                    {
                        firstStarted = true;
                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Thread.Sleep(5);
                        }
                    }

                    return new PreviewEntryContext(
                        string.Empty,
                        string.Empty,
                        new[] { new PreviewContextMetadata("Stable key", entry.Key, "Phoenix Translator") },
                        new PreviewContextRelation[0],
                        new PreviewNpcContext[0],
                        null);
                },
                key => null,
                entry => { });

            inspector.SelectEntry(first);
            AssertEqual(true, SpinWait.SpinUntil(() => firstStarted, 2000),
                "The first synthetic context request must begin before selection changes.");
            inspector.SelectEntry(second);
            AssertEqual(true, SpinWait.SpinUntil(() => inspector.State == PreviewContextState.Ready, 2000),
                "The replacement selection must load after cancelling stale work.");
            AssertEqual("second", inspector.Context.Metadata[0].Value,
                "Cancelled stale work must never replace the current entry context.");
            inspector.Dispose();
        }

        private static void DistinguishesEmptyAndFailedPreviewContext()
        {
            var entry = CreateEntry("entry", "Source", string.Empty);
            var emptyInspector = new PreviewContextInspectorViewModel(
                (selected, cancellationToken) => new PreviewEntryContext(
                    string.Empty,
                    string.Empty,
                    new PreviewContextMetadata[0],
                    new PreviewContextRelation[0],
                    new PreviewNpcContext[0],
                    null),
                key => null,
                selected => { });
            emptyInspector.SelectEntry(entry);
            AssertEqual(true, SpinWait.SpinUntil(() => emptyInspector.State == PreviewContextState.Empty, 2000),
                "Unsupported context must remain an explicit empty state.");
            AssertEqual("No supported context is available for this entry.", emptyInspector.StateText,
                "Unsupported context must not be presented as a failure.");
            emptyInspector.Dispose();

            var failedInspector = new PreviewContextInspectorViewModel(
                (selected, cancellationToken) => { throw new InvalidDataException("Synthetic parser failure."); },
                key => null,
                selected => { });
            failedInspector.SelectEntry(entry);
            AssertEqual(true, SpinWait.SpinUntil(() => failedInspector.State == PreviewContextState.Failed, 2000),
                "Parser failures must reach a distinct failed state.");
            AssertEqual(true, failedInspector.RetryCommand.CanExecute(null),
                "A failed context load must remain explicitly retryable.");
            failedInspector.Dispose();
        }

        private static void ValidatesBoundedPreviewAssets()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PhoenixAssetContextTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string projectPath = Path.Combine(directory, "fixture.xml");
            File.WriteAllText(projectPath, "fixture");
            try
            {
                string imagePath = Path.Combine(directory, "preview.png");
                File.WriteAllBytes(imagePath, Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
                PreviewAssetContext asset = PreviewAssetContextLoader.Load(
                    projectPath,
                    "preview.png",
                    CancellationToken.None);
                AssertEqual("preview.png", asset.DisplayName,
                    "Validated asset context must expose only a safe file name.");
                AssertEqual("1 × 1", asset.Dimensions,
                    "Validated asset context must retain decoded dimensions.");
                AssertEqual(null, PreviewAssetContextLoader.Load(
                    projectPath,
                    "../outside.png",
                    CancellationToken.None),
                    "Asset context must reject traversal outside the project directory.");

                string malformedPath = Path.Combine(directory, "malformed.png");
                File.WriteAllText(malformedPath, "not an image");
                bool malformedRejected = false;
                try
                {
                    PreviewAssetContextLoader.Load(projectPath, "malformed.png", CancellationToken.None);
                }
                catch (InvalidDataException)
                {
                    malformedRejected = true;
                }

                AssertEqual(true, malformedRejected,
                    "Malformed visual assets must fail at the bounded context boundary.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void PreviewsAppliesAndUndoesWorkspaceReplacement()
        {
            var entries = new[]
            {
                CreateEntry("one", "Source one", "old value"),
                CreateEntry("two", "Source two", "another old value")
            };
            PreviewWorkspaceToolsViewModel tools = CreateWorkspaceTools(entries, entries);
            tools.SelectedScope = tools.ScopeOptions.Single(option => option.Value == PreviewWorkspaceToolScope.All);
            tools.FindText = "old";
            tools.ReplacementText = "new";

            AssertEqual(2, tools.PreviewCount, "Replacement preview must report the exact affected entry count.");
            AssertEqual(2, tools.ApplyReplace(), "Replacement must affect the previewed scope.");
            AssertEqual("new value", entries[0].TargetText, "Replacement must stage normalized target text.");
            AssertEqual(true, tools.CanUndo, "A staged workspace tool must expose undo.");

            tools.UndoCommand.Execute(null);
            AssertEqual("old value", entries[0].TargetText, "Undo must restore the exact prior target.");
            AssertEqual("another old value", entries[1].TargetText, "Undo must restore every affected target.");
        }

        private static void RoundTripsBoundedTranslationTable()
        {
            string directory = Path.Combine(Path.GetTempPath(), "NIMTools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "translations.tsv");
                var entries = new[] { CreateEntry("key-1", "Line\nOne", "Target\tOne") };
                PreviewWorkspaceToolsViewModel tools = CreateWorkspaceTools(entries, entries);
                tools.ExportTable(path);
                entries[0].TargetText = string.Empty;

                tools.ImportTable(path);

                AssertEqual("Target\tOne", entries[0].TargetText, "Table import must restore escaped target content by stable key.");
                AssertEqual(true, tools.CanUndo, "Imported table changes must remain undoable before save.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void RoundTripsWorkflowRolloutAndRecoversBackup()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PhoenixRollout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "rollout.xml");
                var store = new PreviewWorkflowRolloutStore(path);
                List<PreviewWorkflowOption> first = PreviewWorkflowRolloutStore.CreateDefaults().ToList();
                first.Single(option => option.Workflow == PreviewWorkflow.History).IsEnabled = false;
                store.Save(first);
                List<PreviewWorkflowOption> second = PreviewWorkflowRolloutStore.CreateDefaults().ToList();
                second.Single(option => option.Workflow == PreviewWorkflow.Quality).IsEnabled = false;
                store.Save(second);
                File.WriteAllText(path, "<invalid>");

                IReadOnlyList<PreviewWorkflowOption> recovered = store.Load();
                AssertEqual(false, recovered.Single(option => option.Workflow == PreviewWorkflow.History).IsEnabled,
                    "A corrupt rollout file must recover the last known-good backup.");
                AssertEqual(true, recovered.Single(option => option.Workflow == PreviewWorkflow.Quality).IsEnabled,
                    "Backup recovery must not partially apply the corrupt primary state.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void RoutesDisabledWorkflowToLegacyFallback()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PhoenixRollout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "rollout.xml");
                var rollout = new PreviewWorkflowRolloutViewModel(new PreviewWorkflowRolloutStore(path));
                rollout.Options.Single(option => option.Workflow == PreviewWorkflow.History).IsEnabled = false;
                rollout.Apply();
                int fallbackCalls = 0;
                var shell = new PreviewShellViewModel(() => fallbackCalls++, rollout, new PreviewDiagnosticService());

                shell.CurrentDestination = PreviewShellDestination.History;

                AssertEqual(1, fallbackCalls, "A disabled workflow must invoke the compatible fallback once.");
                AssertEqual(false, shell.IsHistoryUpdateWorkspaceVisible,
                    "A disabled workflow must not render its preview surface.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void RoundTripsBoundedRamCache()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PhoenixRamCache-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "cache.json");
                var service = new PreviewRamCacheService();
                PreviewTranslationEntry entry = CreateEntry("key-1", "Source", "Imported target");
                service.Export(path, new[] { entry });
                entry.TargetText = string.Empty;

                Dictionary<PreviewTranslationEntry, string> changes = service.ImportDraftTargets(path, new[] { entry });

                AssertEqual("Imported target", entry.TargetText, "RamCache import must match source and stable key.");
                AssertEqual(string.Empty, changes[entry], "RamCache import must retain the prior target for undo.");
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static void ClearsOnlyConfirmedCacheScope()
        {
            bool confirmedProvider = false;
            bool confirmedUser = false;
            bool clearedProvider = false;
            bool clearedUser = false;
            var entries = new[] { CreateEntry("one", "Source", "Target") };
            var tools = new PreviewWorkspaceToolsViewModel(
                () => entries, () => entries, () => entries[0], () => { }, null, null, null, null, null, null,
                null, null, null,
                (provider, user, cancellationToken) =>
                {
                    clearedProvider = provider;
                    clearedUser = user;
                    return Task.FromResult(0);
                },
                (provider, user) =>
                {
                    confirmedProvider = provider;
                    confirmedUser = user;
                    return true;
                });
            tools.ClearProviderCache = true;
            tools.ClearUserCache = false;

            tools.ClearCachesCommand.Execute(null);
            AssertEqual(true, SpinWait.SpinUntil(() => !tools.IsBusy, 2000),
                "Confirmed cache clearing must complete asynchronously.");
            AssertEqual(true, confirmedProvider, "Confirmation must name the selected provider-cache scope.");
            AssertEqual(false, confirmedUser, "Confirmation must exclude unselected user-cache scope.");
            AssertEqual(true, clearedProvider, "Only the confirmed provider cache may be cleared.");
            AssertEqual(false, clearedUser, "The unconfirmed user cache must remain intact.");
        }

        private static void ManagesTranslationHistoryThroughWorkspaceBoundary()
        {
            PreviewTranslationEntry entry = CreateEntry("one", "Source", "Current target");
            var project = new FakePreviewTranslationProject(new[] { entry });
            project.TranslationHistory = new[]
            {
                new PreviewTranslationHistoryItem(7, "one", "Source", "Historical target", false, DateTime.UtcNow)
            };
            var shell = new PreviewShellViewModel(() => { });
            var workspace = new PreviewTranslationWorkspaceViewModel(
                () => project.Path, () => { }, shell, path => project);
            try
            {
                workspace.OpenProjectAsync(project.Path).GetAwaiter().GetResult();
                IReadOnlyList<PreviewTranslationHistoryItem> loaded = workspace
                    .LoadTranslationHistoryAsync(CancellationToken.None).GetAwaiter().GetResult();
                AssertEqual(1, loaded.Count, "History loading must cross the project boundary without a legacy window.");

                workspace.RestoreTranslationHistoryAsync(7, CancellationToken.None).GetAwaiter().GetResult();
                AssertEqual("Historical target", entry.TargetText,
                    "History restoration must stage the selected target in the active workspace.");
                workspace.SetCurrentTranslationHistoryAsync(7).GetAwaiter().GetResult();
                AssertEqual(7, project.CurrentHistoryRowId, "Set current must preserve the selected persistent row.");
                workspace.DeleteTranslationHistoryAsync(7).GetAwaiter().GetResult();
                AssertEqual(0, project.TranslationHistory.Count, "Delete must remove only the selected history row.");
            }
            finally
            {
                workspace.Dispose();
            }
        }

        private static void ValidatesInteractiveProviderRequestIdentity()
        {
            var entries = new[] { CreateEntry("key-1", "Translate me", string.Empty) };
            PreviewWorkspaceToolsViewModel tools = CreateWorkspaceTools(entries, entries);
            tools.PrepareInteractiveCommand.Execute(null);
            Match requestId = Regex.Match(tools.InteractiveRequest, @"<!-- Request ID: ([a-z0-9]+) -->");
            AssertEqual(true, requestId.Success, "Interactive requests must contain a stable correlation identifier.");

            tools.InteractiveResponse = "Translated\r\n<!-- Request ID: wrong -->";
            AssertEqual(false, tools.CanApplyInteractive, "A mismatched provider response must remain blocked.");
            tools.InteractiveResponse = "Translated\r\n<!-- Request ID: " + requestId.Groups[1].Value + " -->";
            AssertEqual(true, tools.CanApplyInteractive, "A matching provider response must be applicable.");
            tools.ApplyInteractiveCommand.Execute(null);
            AssertEqual("Translated", entries[0].TargetText, "The request marker must not enter project content.");
        }

        private static PreviewWorkspaceToolsViewModel CreateWorkspaceTools(
            IReadOnlyList<PreviewTranslationEntry> allEntries,
            IReadOnlyList<PreviewTranslationEntry> visibleEntries,
            Func<string, CancellationToken, string> converter = null)
        {
            return new PreviewWorkspaceToolsViewModel(
                () => allEntries,
                () => visibleEntries,
                () => allEntries.FirstOrDefault(),
                () => { },
                null,
                null,
                null,
                null,
                converter,
                null,
                null);
        }

        private static PreviewTranslationEntry CreateEntry(string key, string source, string target)
        {
            return new PreviewTranslationEntry(key, "XML", key, source, target, 100);
        }

        private static void StagesWritingVariantAndTracksExportReadiness()
        {
            PreviewTranslationEntry entry = CreateEntry("one", "Source", string.Empty);
            PreviewWorkspaceToolsViewModel tools = CreateWorkspaceTools(
                new[] { entry },
                new[] { entry },
                (value, cancellationToken) => "Converted");
            AssertEqual(true, tools.IsExportBlocked, "Draft entries must block project export.");

            tools.ConvertCommand.Execute(null);
            SpinWait.SpinUntil(() => !tools.IsBusy, 2000);
            AssertEqual(string.Empty, entry.TargetText, "Writing conversion must not change content before explicit apply.");
            AssertEqual(true, tools.CanApplyConversion, "A completed conversion preview must be explicitly applicable.");
            tools.ApplyConversionCommand.Execute(null);
            AssertEqual("Converted", entry.TargetText, "Applying a conversion preview must stage the converted target.");
            AssertEqual(false, tools.IsExportBlocked, "A complete non-rejected project must pass the blocking readiness gate.");
            entry.SetReviewState(PreviewReviewState.Approved);
            tools.RefreshReadiness();
            AssertEqual(
                PreviewMessageCatalog.Get("Workspace_Tools_Export_Ready"),
                tools.ExportReadinessText,
                "Approved content must report ready export state.");
        }

        private static void AnalyzesMixedQualityFindings()
        {
            var entries = new[]
            {
                new PreviewTranslationEntry("1", "MCM", "GREETING", "Hello {0}", "Hallo", 100),
                new PreviewTranslationEntry("2", "PEX", "IDENTIFIER", "MENU_FILE", "Menü", 100),
                new PreviewTranslationEntry("3", "XML", "DUPLICATE_A", "Same source", "First", 100),
                new PreviewTranslationEntry("4", "XML", "DUPLICATE_B", "Same source", "Second", 100),
                new PreviewTranslationEntry("5", "ESP", "EMPTY", "Needs translation", "", 25)
            };

            IReadOnlyList<PreviewQualityFinding> findings = new PreviewQualityAnalyzer().Analyze(entries);

            AssertEqual(true, findings.Any(finding => finding.RuleId == "placeholder-mismatch" && finding.IsBlocking),
                "A missing source placeholder must block export.");
            AssertEqual(true, findings.Any(finding => finding.RuleId == "technical-string" &&
                finding.Severity == PreviewFindingSeverity.Warning),
                "Identifier-like text must be exposed as an acknowledgeable warning.");
            AssertEqual(2, findings.Count(finding => finding.RuleId == "duplicate-inconsistent"),
                "Every affected duplicate must identify its exact entry.");
            AssertEqual(true, findings.Any(finding => finding.RuleId == "untranslated" && finding.Entry.Key == "5"),
                "An untranslated entry must remain an explicit blocking finding.");
            AssertEqual(true, findings.Any(finding => finding.RuleId == "low-confidence" && finding.Source == "ESP"),
                "Format confidence findings must retain their diagnostic source.");
        }

        private static void TracksReviewDecisionsAndBulkUndo()
        {
            string stateDirectory = Path.Combine(Path.GetTempPath(), "PhoenixReviewTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stateDirectory);
            try
            {
                var entries = new[]
                {
                    new PreviewTranslationEntry("1", "MCM", "ONE", "First", "Erste", 100),
                    new PreviewTranslationEntry("2", "MCM", "TWO", "Second", "Zweite", 100)
                };
                var shell = new PreviewShellViewModel(() => { });
                var project = new FakePreviewTranslationProject(entries, Path.Combine(stateDirectory, "project.xml"));
                var workspace = new PreviewTranslationWorkspaceViewModel(() => project.Path, () => { }, shell, path => project);
                workspace.OpenProjectAsync(project.Path).GetAwaiter().GetResult();
                var cancelled = new PreviewReviewQualityViewModel(shell, workspace, new PreviewQualityAnalyzer(),
                    new PreviewReviewStateStore(stateDirectory), count => false, () => { });
                cancelled.RevalidateAsync().GetAwaiter().GetResult();

                cancelled.ApproveScopeCommand.Execute(null);
                AssertEqual(PreviewReviewState.Unreviewed, entries[0].ReviewState,
                    "Cancelling bulk approval must preserve every review decision.");
                cancelled.Dispose();

                var review = new PreviewReviewQualityViewModel(shell, workspace, new PreviewQualityAnalyzer(),
                    new PreviewReviewStateStore(stateDirectory), count => count == 2, () => { });
                review.RevalidateAsync().GetAwaiter().GetResult();
                review.ApproveScopeCommand.Execute(null);
                AssertEqual(PreviewReviewState.Approved, entries[0].ReviewState,
                    "Confirmed bulk approval must update the visible eligible scope.");
                AssertEqual(PreviewReviewState.Approved, entries[1].ReviewState,
                    "Confirmed bulk approval must update every visible eligible entry.");

                review.UndoBulkCommand.Execute(null);
                AssertEqual(PreviewReviewState.Unreviewed, entries[0].ReviewState,
                    "Undo must restore the review state captured before bulk approval.");
                AssertEqual(PreviewReviewState.Unreviewed, entries[1].ReviewState,
                    "Undo must restore the complete bulk scope.");
                review.Dispose();
                workspace.Dispose();
            }
            finally
            {
                Directory.Delete(stateDirectory, true);
            }
        }

        private static void PersistsPrivateReviewMetadata()
        {
            string stateDirectory = Path.Combine(Path.GetTempPath(), "PhoenixReviewTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stateDirectory);
            try
            {
                string projectPath = Path.Combine(stateDirectory, "private-project.xml");
                const string privateTarget = "Private translated content";
                var store = new PreviewReviewStateStore(stateDirectory);
                var entry = new PreviewTranslationEntry("entry-1", "XML", "RECORD", "Source", privateTarget, 100);
                entry.SetReviewState(PreviewReviewState.Approved);
                store.Save(projectPath, new[] { entry }, new[] { "technical-string:entry-1" });

                string persistedText = File.ReadAllText(Directory.GetFiles(stateDirectory, "*.xml").Single());
                AssertEqual(false, persistedText.Contains(projectPath),
                    "Review metadata must not contain an absolute project path.");
                AssertEqual(false, persistedText.Contains(privateTarget),
                    "Review metadata must not contain private translated content.");

                PreviewReviewStateSnapshot snapshot = store.Load(projectPath);
                AssertEqual(PreviewReviewState.Approved, snapshot.Decisions["entry-1"].State,
                    "A matching project and target fingerprint must restore its review decision.");
                AssertEqual(true, snapshot.AcknowledgedFindingIds.Contains("technical-string:entry-1"),
                    "Acknowledged warning identity must persist without warning content.");

                entry.TargetText = "Changed target";
                AssertEqual(PreviewReviewState.Unreviewed, entry.ReviewState,
                    "Editing approved content must invalidate its human review decision.");
            }
            finally
            {
                Directory.Delete(stateDirectory, true);
            }
        }

        private static void NavigatesFromFindingToTranslationEntry()
        {
            string stateDirectory = Path.Combine(Path.GetTempPath(), "PhoenixReviewTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stateDirectory);
            try
            {
                var entries = new[]
                {
                    new PreviewTranslationEntry("1", "PEX", "MENU_FILE", "MENU_FILE", "Menü", 100),
                    new PreviewTranslationEntry("2", "XML", "OTHER", "Other", "Andere", 100)
                };
                var shell = new PreviewShellViewModel(() => { });
                var project = new FakePreviewTranslationProject(entries, Path.Combine(stateDirectory, "project.xml"));
                var workspace = new PreviewTranslationWorkspaceViewModel(() => project.Path, () => { }, shell, path => project);
                workspace.OpenProjectAsync(project.Path).GetAwaiter().GetResult();
                var review = new PreviewReviewQualityViewModel(shell, workspace, new PreviewQualityAnalyzer(),
                    new PreviewReviewStateStore(stateDirectory), count => true, () => { });
                review.RevalidateAsync().GetAwaiter().GetResult();
                shell.CurrentDestination = PreviewShellDestination.Quality;
                review.SelectedFinding = review.Findings.Single(finding => finding.RuleId == "technical-string");

                review.AcknowledgeFindingCommand.Execute(null);
                AssertEqual("Ready with acknowledged warnings", review.ExportReadinessText,
                    "Acknowledged non-blocking warnings must be distinguished from open warnings.");
                review.GoToEntryCommand.Execute(null);
                AssertEqual(PreviewShellDestination.Translate, shell.CurrentDestination,
                    "Finding navigation must return to the translation workspace.");
                AssertEqual(entries[0], workspace.SelectedEntry,
                    "Finding navigation must reveal the exact affected entry.");
                review.Dispose();
                workspace.Dispose();
            }
            finally
            {
                Directory.Delete(stateDirectory, true);
            }
        }

        private static void ClassifiesProjectRevisionChanges()
        {
            var current = new[]
            {
                new PreviewTranslationEntry("added", "XML", "ADDED", "New", string.Empty, 100),
                new PreviewTranslationEntry("reusable", "XML", "REUSABLE", "Same", string.Empty, 100),
                new PreviewTranslationEntry("changed", "XML", "CHANGED", "New source", string.Empty, 100),
                new PreviewTranslationEntry("unchanged", "XML", "UNCHANGED", "Stable", "Stabil", 100)
            };
            var previous = new[]
            {
                new PreviewTranslationEntry("unchanged", "XML", "UNCHANGED", "Stable", "Stabil", 100),
                new PreviewTranslationEntry("removed", "XML", "REMOVED", "Old", "Alt", 100),
                new PreviewTranslationEntry("changed", "XML", "CHANGED", "Old source", "Alte Quelle", 100),
                new PreviewTranslationEntry("reusable", "XML", "REUSABLE", "Same", "Gleich", 100)
            };

            IReadOnlyList<PreviewProjectComparisonItem> result =
                new PreviewProjectComparisonService().Compare(current, previous);

            AssertEqual(PreviewRevisionComparisonState.Added,
                result.Single(item => item.Key == "added").State,
                "A current-only stable identity must be classified as added.");
            AssertEqual(PreviewRevisionComparisonState.Removed,
                result.Single(item => item.Key == "removed").State,
                "A previous-only stable identity must be classified as removed.");
            AssertEqual(PreviewRevisionComparisonState.Reusable,
                result.Single(item => item.Key == "reusable").State,
                "An untranslated equal source must expose the prior target as reusable.");
            AssertEqual(PreviewRevisionComparisonState.Changed,
                result.Single(item => item.Key == "changed").State,
                "A changed source without a current target must require fresh translation.");
            AssertEqual(PreviewRevisionComparisonState.Unchanged,
                result.Single(item => item.Key == "unchanged").State,
                "Equal source and target content must remain unchanged regardless of source order.");

            PreviewProjectComparisonItem ambiguous = new PreviewProjectComparisonService().Compare(
                new[]
                {
                    new PreviewTranslationEntry("duplicate", "XML", "ONE", "First", string.Empty, 100),
                    new PreviewTranslationEntry("duplicate", "XML", "TWO", "Second", string.Empty, 100)
                },
                new[] { new PreviewTranslationEntry("duplicate", "XML", "OLD", "First", "Erste", 100) })
                .Single();
            AssertEqual(PreviewRevisionComparisonState.Conflict, ambiguous.State,
                "Ambiguous stable identities must be exposed as conflicts instead of being silently discarded.");
        }

        private static void PreservesReviewedTargetsAsConflicts()
        {
            var current = new PreviewTranslationEntry("entry", "PEX", "ENTRY", "Source", "Current", 100);
            current.SetReviewState(PreviewReviewState.Approved);
            var previous = new PreviewTranslationEntry("entry", "PEX", "ENTRY", "Source", "Previous", 100);

            PreviewProjectComparisonItem result = new PreviewProjectComparisonService()
                .Compare(new[] { current }, new[] { previous }).Single();

            AssertEqual(PreviewRevisionComparisonState.Conflict, result.State,
                "Different populated targets must never be silently reusable.");
            AssertEqual(true, result.CanReuse,
                "A conflicting prior target may remain available for an explicit confirmed decision.");
            AssertEqual("Current", current.TargetText,
                "Classification must not alter a reviewed current target.");

            current.ApplyReusedTarget(previous.TargetText);
            AssertEqual(PreviewReviewState.Unreviewed, current.ReviewState,
                "Explicit target reuse must invalidate the previous review decision.");
            AssertEqual("Previous project revision", current.Provenance,
                "Explicit target reuse must expose revision provenance.");
        }

        private static void PersistsPrivateRevisionHistory()
        {
            string stateDirectory = Path.Combine(Path.GetTempPath(), "PhoenixHistoryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stateDirectory);
            try
            {
                string projectPath = Path.Combine(stateDirectory, "private-project.xml");
                const string privateSource = "Private source content";
                const string privateTarget = "Private target content";
                var entry = new PreviewTranslationEntry("entry", "XML", "RECORD", privateSource, privateTarget, 100);
                var store = new PreviewRevisionHistoryStore(stateDirectory);
                store.Append(projectPath, PreviewRevisionHistoryStore.Create("Reused", entry, DateTime.UtcNow));

                string persistedText = File.ReadAllText(Directory.GetFiles(stateDirectory, "*.xml").Single());
                AssertEqual(false, persistedText.Contains(projectPath),
                    "Revision history must not contain an absolute project path.");
                AssertEqual(false, persistedText.Contains(privateSource),
                    "Revision history must not contain private source content.");
                AssertEqual(false, persistedText.Contains(privateTarget),
                    "Revision history must not contain private target content.");
                AssertEqual(1, store.Load(projectPath).Count,
                    "A valid privacy-preserving history event must round-trip.");
            }
            finally
            {
                Directory.Delete(stateDirectory, true);
            }
        }

        private static void AppliesAndUndoesExplicitRevisionReuse()
        {
            string stateDirectory = Path.Combine(Path.GetTempPath(), "PhoenixUpdateTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stateDirectory);
            try
            {
                string currentPath = Path.Combine(stateDirectory, "current.xml");
                string previousPath = Path.Combine(stateDirectory, "previous.xml");
                var reusable = new PreviewTranslationEntry("reusable", "XML", "REUSABLE", "Same", string.Empty, 100);
                var conflict = new PreviewTranslationEntry("conflict", "XML", "CONFLICT", "Stable", "Current", 100);
                conflict.SetReviewState(PreviewReviewState.Approved);
                var currentProject = new FakePreviewTranslationProject(new[] { reusable, conflict }, currentPath);
                var previousProject = new FakePreviewTranslationProject(new[]
                {
                    new PreviewTranslationEntry("conflict", "XML", "CONFLICT", "Stable", "Previous", 100),
                    new PreviewTranslationEntry("reusable", "XML", "REUSABLE", "Same", "Reusable target", 100)
                }, previousPath);
                var shell = new PreviewShellViewModel(() => { });
                var workspace = new PreviewTranslationWorkspaceViewModel(
                    () => currentPath, () => { }, shell, path => currentProject);
                workspace.OpenProjectAsync(currentPath).GetAwaiter().GetResult();
                var update = new PreviewHistoryUpdateViewModel(
                    shell,
                    workspace,
                    new PreviewProjectComparisonService(),
                    new PreviewRevisionHistoryStore(stateDirectory),
                    () => previousPath,
                    path => previousProject,
                    count => false,
                    count => true,
                    () => { });

                update.CompareRevisionCommand.Execute(null);
                DateTime timeout = DateTime.UtcNow.AddSeconds(5);
                while (!update.HasComparison && DateTime.UtcNow < timeout)
                {
                    System.Threading.Thread.Sleep(10);
                }

                AssertEqual(true, update.HasComparison,
                    "A compatible selected revision must produce comparison results.");
                update.SelectedComparisonItem = update.ComparisonItems.Single(item => item.Key == "conflict");
                update.ReuseSelectedCommand.Execute(null);
                AssertEqual("Current", conflict.TargetText,
                    "Cancelling conflict confirmation must preserve the reviewed current target.");
                AssertEqual(PreviewReviewState.Approved, conflict.ReviewState,
                    "Cancelling conflict confirmation must preserve its review decision.");

                update.SelectedComparisonItem = update.ComparisonItems.Single(item => item.Key == "reusable");
                update.ReuseSelectedCommand.Execute(null);
                AssertEqual("Reusable target", reusable.TargetText,
                    "Explicit safe reuse must stage the selected prior target.");
                AssertEqual(PreviewReviewState.Unreviewed, reusable.ReviewState,
                    "Reused content must enter review as unreviewed.");

                update.UndoCommand.Execute(null);
                AssertEqual(string.Empty, reusable.TargetText,
                    "Undo must restore the target captured before explicit reuse.");
                AssertEqual(true, update.HistoryEntries.Any(entry => entry.ActionId == "Reused"),
                    "Explicit reuse must create a project history event.");
                AssertEqual(true, update.HistoryEntries.Any(entry => entry.ActionId == "Undone"),
                    "Undo must create a project history event.");
                update.Dispose();
                workspace.Dispose();
            }
            finally
            {
                Directory.Delete(stateDirectory, true);
            }
        }

        private static void StagesAndCancelsUnifiedSettings()
        {
            var store = new RecordingPreviewSettingsStore();
            var shell = new PreviewShellViewModel(() => { });
            var settings = new PreviewSettingsViewModel(shell, store, () => true, () => true, () => { });

            settings.Settings.ContextLimitText = "500";
            AssertEqual(true, settings.IsModified,
                "Editing one centralized setting must stage a modified state.");
            AssertEqual(0, store.SaveCalls,
                "Editing staged settings must not persist through legacy immediate-save handlers.");

            settings.CancelCommand.Execute(null);
            AssertEqual("200", settings.Settings.ContextLimitText,
                "Cancel must restore the complete loaded snapshot.");
            AssertEqual(false, settings.IsModified,
                "Cancel must restore the clean state.");
            AssertEqual(0, store.SaveCalls,
                "Cancel must not persist any staged value.");
            settings.Dispose();
        }

        private static void ValidatesAndAppliesUnifiedSettings()
        {
            var store = new RecordingPreviewSettingsStore();
            var shell = new PreviewShellViewModel(() => { });
            var settings = new PreviewSettingsViewModel(shell, store, () => true, () => true, () => { });

            settings.Settings.MaxThreadCountText = "0";
            AssertEqual(true, settings.HasValidationErrors,
                "An invalid worker limit must be explained before persistence.");
            settings.ApplyCommand.Execute(null);
            AssertEqual(0, store.SaveCalls,
                "Invalid settings must never reach persistence.");

            settings.Settings.MaxThreadCountText = "4";
            settings.Settings.EnableGlobalSearch = true;
            settings.ApplyCommand.Execute(null);
            AssertEqual(1, store.SaveCalls,
                "One explicit Apply action must persist the complete valid snapshot exactly once.");
            AssertEqual(true, store.Current.EnableGlobalSearch,
                "Apply must persist staged values through the central store boundary.");
            AssertEqual(false, settings.IsModified,
                "Successful Apply must establish a new clean baseline.");
            settings.Dispose();
        }

        private static void SearchesSettingsByLegacyTerminology()
        {
            var store = new RecordingPreviewSettingsStore();
            var shell = new PreviewShellViewModel(() => { });
            var settings = new PreviewSettingsViewModel(shell, store, () => true, () => true, () => { });

            settings.SearchText = "node";
            AssertEqual(1, settings.VisibleCategories.Count,
                "Legacy provider-node terminology must resolve to one central category.");
            AssertEqual(PreviewSettingsCategory.Providers, settings.VisibleCategories[0].Value,
                "Provider nodes must resolve to Providers instead of another settings window.");

            settings.SearchText = "dictionary";
            AssertEqual(PreviewSettingsCategory.HistoryAndData, settings.VisibleCategories.Single().Value,
                "Legacy dictionary terminology must route to History and data.");
            settings.Dispose();
        }

        private static void ProtectsSettingsSecrets()
        {
            var store = new RecordingPreviewSettingsStore();
            var shell = new PreviewShellViewModel(() => { });
            var settings = new PreviewSettingsViewModel(shell, store, () => true, () => true, () => { });
            const string secret = "private-provider-secret";

            settings.StageProviderCredential(secret);
            AssertEqual(false, settings.Settings.GetType().GetProperties()
                    .Any(property => string.Equals(property.GetValue(settings.Settings) as string, secret, StringComparison.Ordinal)),
                "A staged credential must not be exposed through bindable settings properties.");
            AssertEqual(false, settings.ValidationMessages.Any(message => message.Contains(secret)),
                "Validation output must never contain a staged credential.");

            settings.Settings.EnableGlobalSearch = true;
            settings.ApplyCommand.Execute(null);
            AssertEqual(secret, store.LastProviderCredential,
                "The write-only store boundary must receive the explicitly staged credential.");
            AssertEqual(false, settings.Settings.GetType().GetProperties()
                    .Any(property => string.Equals(property.GetValue(settings.Settings) as string, secret, StringComparison.Ordinal)),
                "Reloaded settings must expose only credential presence, never its value.");
            settings.Dispose();
        }

        private static void TestsProviderWithoutSavingSettings()
        {
            var store = new RecordingPreviewSettingsStore();
            var shell = new PreviewShellViewModel(() => { });
            var settings = new PreviewSettingsViewModel(shell, store, () => true, () => true, () => { });
            const string secret = "staged-test-secret";

            settings.StageProviderCredential(secret);
            settings.TestProviderCommand.Execute(null);

            AssertEqual(1, store.TestCalls,
                "An explicit provider test must cross the connectivity boundary once.");
            AssertEqual(0, store.SaveCalls,
                "Testing staged provider settings must not persist them.");
            AssertEqual(secret, store.LastTestCredential,
                "The connectivity boundary must receive a staged credential without exposing it to binding.");
            AssertEqual(PreviewMessageCatalog.Get("Settings_Providers_Test_Succeeded"), settings.ProviderTestStatusText,
                "A successful provider test must produce a sanitized status.");
            settings.Dispose();
        }

        private static void ClearsStagedCredentialWhenProviderChanges()
        {
            var store = new RecordingPreviewSettingsStore();
            var shell = new PreviewShellViewModel(() => { });
            var settings = new PreviewSettingsViewModel(shell, store, () => true, () => true, () => { });

            settings.StageProviderCredential("credential-for-first-provider");
            settings.SelectedProvider = settings.ProviderOptions[1];
            settings.TestProviderCommand.Execute(null);

            AssertEqual(0, store.TestCalls,
                "A staged credential must not follow the user to another provider configuration.");
            AssertEqual(true, settings.ProviderTestStatusText.Contains(
                    PreviewMessageCatalog.Get("Settings_Validation_CredentialRequired")),
                "The newly selected provider must require its own credential.");
            settings.Dispose();
        }

        private static void StagesAndAppliesProviderPipeline()
        {
            var store = new RecordingAdvancedToolsStore();
            var tools = new PreviewAdvancedToolsViewModel(store, new PreviewShellViewModel(() => { }), () => true);
            PreviewPipelineEntry second = tools.PipelineEntries[1];
            tools.SelectedPipelineEntry = second;
            tools.MoveUpCommand.Execute(null);
            second.IsEnabled = true;

            AssertEqual(0, store.SavePipelineCalls, "Pipeline editing must remain staged before apply.");
            tools.ApplyPipelineCommand.Execute(null);
            AssertEqual(1, store.SavePipelineCalls, "Applying must cross the persistence boundary exactly once.");
            AssertEqual(2, store.SavedPipeline[0].Key, "Applying must preserve the staged provider order.");
            AssertEqual(true, store.SavedPipeline[0].IsEnabled, "Applying must preserve staged enablement.");
            tools.Dispose();
        }

        private static void ValidatesCustomProviderDrafts()
        {
            var valid = new PreviewCustomProviderDraft
            {
                Name = "Fixture provider",
                Endpoint = "https://provider.invalid/v1",
                ResponseField = "choices[0].message.content"
            };
            AssertEqual(null, PreviewAdvancedToolsViewModel.ValidateCustomProvider(valid),
                "A complete HTTPS provider draft must be valid.");
            valid.Endpoint = "http://provider.invalid/v1";
            AssertEqual("Advanced_Custom_Validation_Endpoint", PreviewAdvancedToolsViewModel.ValidateCustomProvider(valid),
                "Plain HTTP must be limited to local endpoints.");
            valid.Endpoint = "http://127.0.0.1:1234/v1";
            AssertEqual(null, PreviewAdvancedToolsViewModel.ValidateCustomProvider(valid),
                "A loopback HTTP provider must remain supported.");
        }

        private static void CancelsCustomProviderConnectivityTest()
        {
            var store = new RecordingAdvancedToolsStore { BlockProviderTest = true };
            var tools = new PreviewAdvancedToolsViewModel(store, new PreviewShellViewModel(() => { }), () => true);
            tools.CustomProvider.Name = "Fixture provider";
            tools.CustomProvider.Endpoint = "https://provider.invalid/v1";
            tools.CustomProvider.ResponseField = "translation";
            tools.TestCustomProviderCommand.Execute(null);
            AssertEqual(true, SpinWait.SpinUntil(() => tools.IsTesting, 1000), "The provider test must start asynchronously.");
            tools.CancelCustomProviderTestCommand.Execute(null);
            AssertEqual(true, SpinWait.SpinUntil(() => !tools.IsTesting, 1000), "Cancellation must complete the provider test.");
            AssertEqual(PreviewMessageCatalog.Get("Advanced_Custom_Test_Cancelled"), tools.StatusText,
                "Cancellation must expose a sanitized status.");
            tools.Dispose();
        }

        private static void GuardsReadOnlyDatabaseStatements()
        {
            AssertEqual(true, PreviewDatabaseStatementGuard.IsReadOnly(" SELECT * FROM Dictionary"),
                "A SELECT query must be allowed in read-only mode.");
            AssertEqual(false, PreviewDatabaseStatementGuard.IsReadOnly("DELETE FROM Dictionary"),
                "A mutation must be rejected in read-only mode.");
            AssertEqual(false, PreviewDatabaseStatementGuard.IsReadOnly("SELECT * FROM Dictionary; DELETE FROM Dictionary"),
                "A second statement must be rejected in read-only mode.");
        }

        private static void ExecutesDatabaseStatementsInsideAdvancedWorkspace()
        {
            var store = new RecordingAdvancedToolsStore();
            int confirmations = 0;
            var tools = new PreviewAdvancedToolsViewModel(
                store,
                new PreviewShellViewModel(() => { }),
                () => { confirmations++; return true; });

            tools.ExecuteDatabaseQueryCommand.Execute(null);
            AssertEqual(false, store.LastDatabaseMutation,
                "The embedded database workspace must start in read-only mode.");
            AssertEqual(1, tools.DatabaseRows.Count,
                "Bounded database results must render in the Advanced Tools workspace.");

            tools.OpenDatabaseMutationCommand.Execute(null);
            tools.DatabaseQuery = "DELETE FROM Fixture WHERE Rowid = 1";
            tools.ExecuteDatabaseQueryCommand.Execute(null);
            AssertEqual(1, confirmations, "Mutation mode must require one explicit destructive confirmation.");
            AssertEqual(true, store.LastDatabaseMutation,
                "Only the confirmed mode may cross the mutation boundary.");
            tools.Dispose();
        }

        private static void ValidatesSemanticIconRegistry()
        {
            IReadOnlyList<PreviewIconDefinition> definitions = PreviewIconRegistry.GetDefinitions();
            int enumCount = Enum.GetValues(typeof(PreviewIconName)).Length;
            AssertEqual(enumCount, definitions.Count, "Every semantic icon must have exactly one generated mapping.");
            AssertEqual(enumCount, definitions.Select(definition => definition.Codepoint).Distinct().Count(),
                "The subset must not register duplicate glyphs.");
            AssertEqual(enumCount, definitions.Select(definition => definition.SemanticName).Distinct().Count(),
                "Semantic icon names must be unique.");
            AssertEqual(true, definitions.All(definition => !string.IsNullOrWhiteSpace(definition.Fallback)),
                "Every semantic icon must have a text fallback.");
            AssertEqual("?", PreviewIconRegistry.Resolve((PreviewIconName)999, false),
                "Unknown semantic identifiers must resolve to a safe text fallback.");
        }

        private static TranslationPresetCoordinator CreateCoordinator(RecordingStore store)
        {
            return new TranslationPresetCoordinator(new TranslationPresetService(), store);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(string.Format(
                    "{0} Expected <{1}> but received <{2}>.",
                    message,
                    expected,
                    actual));
            }
        }

        private sealed class RecordingStore : ITranslationPresetStore
        {
            internal RecordingStore(TranslationPreset preset, TranslationPresetSettings settings)
            {
                Preset = preset;
                Settings = settings;
            }

            public TranslationPreset Preset { get; set; }

            internal TranslationPresetSettings Settings { get; private set; }

            internal int SaveCalls { get; private set; }

            public TranslationPresetSettings ReadSettings()
            {
                return Settings;
            }

            public void ApplySettings(TranslationPresetSettings settings)
            {
                Settings = settings;
            }

            public void Save()
            {
                SaveCalls++;
            }
        }

        private sealed class FakePreviewTranslationProject : IPreviewTranslationProject
        {
            internal FakePreviewTranslationProject(IReadOnlyList<PreviewTranslationEntry> entries, string path = "fixture.xml")
            {
                Entries = entries;
                Path = path;
            }

            public string Path { get; private set; }

            public string DisplayName => "fixture.xml";

            public IReadOnlyList<PreviewTranslationEntry> Entries { get; private set; }

            internal string FailedKey { get; set; }

            internal bool BlockUntilCancelled { get; set; }

            internal bool TranslateStarted { get; private set; }

            public string Translate(PreviewTranslationEntry entry, System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TranslateStarted = true;
                if (BlockUntilCancelled)
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        System.Threading.Thread.Sleep(5);
                    }
                }

                if (string.Equals(entry.Key, FailedKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Synthetic provider failure.");
                }

                return "Translated " + entry.SourceText;
            }

            public PreviewEntryContext LoadContext(
                PreviewTranslationEntry entry,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Context ?? new PreviewEntryContext(
                    string.Empty,
                    string.Empty,
                    new PreviewContextMetadata[0],
                    new PreviewContextRelation[0],
                    new PreviewNpcContext[0],
                    null);
            }

            internal PreviewEntryContext Context { get; set; }

            public void Save()
            {
            }

            public void Export(string path, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExportPath = path;
            }

            public void ClearTranslationCaches(
                bool clearProviderCache,
                bool clearUserCache,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ClearedProviderCache = clearProviderCache;
                ClearedUserCache = clearUserCache;
            }

            internal bool ClearedProviderCache { get; private set; }

            internal bool ClearedUserCache { get; private set; }

            public IReadOnlyList<PreviewTranslationHistoryItem> LoadTranslationHistory(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return TranslationHistory;
            }

            public PreviewTranslationEntry RestoreTranslationHistory(
                int rowId,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PreviewTranslationHistoryItem item = TranslationHistory.FirstOrDefault(entry => entry.RowId == rowId);
                PreviewTranslationEntry projectEntry = Entries.FirstOrDefault(entry => entry.Key == item?.EntryKey);
                if (projectEntry != null)
                {
                    projectEntry.TargetText = item.TargetText;
                }
                return projectEntry;
            }

            public void SetCurrentTranslationHistory(int rowId)
            {
                CurrentHistoryRowId = rowId;
            }

            public void DeleteTranslationHistory(int rowId)
            {
                TranslationHistory = TranslationHistory.Where(entry => entry.RowId != rowId).ToList();
            }

            public void ClearTranslationHistory()
            {
                TranslationHistory = new List<PreviewTranslationHistoryItem>();
            }

            internal IReadOnlyList<PreviewTranslationHistoryItem> TranslationHistory { get; set; } =
                new List<PreviewTranslationHistoryItem>();

            internal int CurrentHistoryRowId { get; private set; }

            public string ExportPath { get; private set; }

            public void Dispose()
            {
            }
        }

        private sealed class RecordingPreviewDialogService : IPreviewDialogService
        {
            internal RecordingPreviewDialogService()
            {
                Requests = new List<PreviewDialogRequest>();
            }

            internal IList<PreviewDialogRequest> Requests { get; private set; }

            public bool Show(PreviewDialogRequest request)
            {
                Requests.Add(request);
                return true;
            }
        }

        private sealed class RecordingRecentProjectStore : IPreviewRecentProjectStore
        {
            private IReadOnlyList<PreviewRecentProject> _projects = new PreviewRecentProject[0];

            internal int SaveCalls { get; private set; }

            public IReadOnlyList<PreviewRecentProject> Load()
            {
                return _projects;
            }

            public void Save(IReadOnlyList<PreviewRecentProject> projects)
            {
                SaveCalls++;
                _projects = projects.ToArray();
            }
        }

        private sealed class RecordingPreviewSettingsStore : IPreviewSettingsStore
        {
            internal RecordingPreviewSettingsStore()
            {
                Current = new PreviewSettingsSnapshot
                {
                    ProviderKey = 1,
                    ProviderModel = "test-model",
                    ProviderEnabled = false,
                    HasStoredCredential = true,
                    LocalPortText = "1234",
                    SourceLanguage = "English",
                    TargetLanguage = "English",
                    EnableLanguageDetection = true,
                    EnableContext = true,
                    ContextLimitText = "200",
                    PlaceholderPattern = "<(.*?)>,",
                    GenerateCSharp = true,
                    UiLanguage = "English",
                    Density = "Compact",
                    MaxThreadCountText = "2",
                    ThrottleRatioText = "0.7",
                    ThrottleDelayText = "200"
                };
            }

            internal PreviewSettingsSnapshot Current { get; private set; }
            internal int SaveCalls { get; private set; }
            internal int TestCalls { get; private set; }
            internal string LastProviderCredential { get; private set; }
            internal string LastTestCredential { get; private set; }

            public IReadOnlyList<PreviewProviderOption> GetProviders()
            {
                return new[]
                {
                    new PreviewProviderOption(1, "Test provider", false, true, true, new[] { "test-model" }),
                    new PreviewProviderOption(2, "Second provider", false, false, true, new[] { "other-model" })
                };
            }

            public IReadOnlyList<string> GetLanguages()
            {
                return new[] { "English", "German" };
            }

            public PreviewSettingsSnapshot Load()
            {
                return Current.Clone();
            }

            public void Save(PreviewSettingsSnapshot settings, string providerCredential, string proxyPassword)
            {
                SaveCalls++;
                Current = settings.Clone();
                if (!string.IsNullOrWhiteSpace(providerCredential))
                {
                    LastProviderCredential = providerCredential;
                    Current.HasStoredCredential = true;
                }

                if (!string.IsNullOrWhiteSpace(proxyPassword))
                {
                    Current.HasStoredProxyPassword = true;
                }
            }

            public Task<PreviewProviderTestResult> TestProviderAsync(
                PreviewSettingsSnapshot settings,
                string providerCredential,
                string proxyPassword,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TestCalls++;
                LastTestCredential = providerCredential;
                return Task.FromResult(new PreviewProviderTestResult(PreviewProviderTestStatus.Succeeded));
            }
        }

        private sealed class RecordingAdvancedToolsStore : IPreviewAdvancedToolsStore
        {
            internal int SavePipelineCalls { get; private set; }
            internal IReadOnlyList<PreviewPipelineEntry> SavedPipeline { get; private set; }
            internal bool BlockProviderTest { get; set; }
            internal bool LastDatabaseMutation { get; private set; }

            public IReadOnlyList<PreviewPipelineEntry> LoadPipeline()
            {
                return new[]
                {
                    new PreviewPipelineEntry(1, "First", "Cloud AI", true, false),
                    new PreviewPipelineEntry(2, "Second", "Local AI", false, false)
                };
            }

            public void SavePipeline(IReadOnlyList<PreviewPipelineEntry> entries)
            {
                SavePipelineCalls++;
                SavedPipeline = entries.Select(entry => entry.Clone()).ToArray();
            }

            public async Task<PreviewProviderTestResult> TestCustomProviderAsync(
                PreviewCustomProviderDraft draft,
                CancellationToken cancellationToken)
            {
                if (BlockProviderTest)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                return new PreviewProviderTestResult(PreviewProviderTestStatus.Succeeded);
            }

            public void SaveCustomProvider(PreviewCustomProviderDraft draft) { }
            public IReadOnlyList<PreviewDatabaseResultRow> ExecuteDatabaseQuery(string sql, bool allowMutation)
            {
                LastDatabaseMutation = allowMutation;
                return new[] { new PreviewDatabaseResultRow("Fixture: value") };
            }
            public IReadOnlyList<KeyValuePair<string, long>> ReadTokenUsage() { return new[] { new KeyValuePair<string, long>("Fixture", 12) }; }
            public void ClearTokenUsage() { }
        }
    }
}
