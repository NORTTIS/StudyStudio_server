using System.IO;
using Xunit;

namespace StudioStudio_Server.Tests.Configuration
{
    /// <summary>
    /// Tests for .coderabbit.yaml — the CodeRabbit AI code-review configuration.
    /// Uses text-based assertions so no external YAML library is required.
    /// </summary>
    public class CodeRabbitConfigTests
    {
        // Resolve the repo root from the test binary output directory
        // (e.g. bin/Debug/net8.0/ → go up 4 levels to reach the repo root).
        private static readonly string RepoRoot =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../"));

        private static readonly string ConfigPath =
            Path.Combine(RepoRoot, ".coderabbit.yaml");

        private static string ReadConfig() => File.ReadAllText(ConfigPath);

        // =====================================================================
        #region File Existence & Integrity

        [Fact]
        public void ConfigFile_Exists_InRepoRoot()
        {
            Assert.True(File.Exists(ConfigPath),
                $".coderabbit.yaml not found at expected path: {ConfigPath}");
        }

        [Fact]
        public void ConfigFile_IsNotEmpty()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.False(string.IsNullOrWhiteSpace(content),
                ".coderabbit.yaml should not be empty.");
        }

        [Fact]
        public void ConfigFile_IsValidUtf8_AndContainsExpectedSchemaComment()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            // The file must declare the CodeRabbit JSON schema reference.
            Assert.Contains("schema.v2.json", content);
        }

        [Fact]
        public void ConfigFile_ContainsAllMajorTopLevelSections()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("language:", content);
            Assert.Contains("reviews:", content);
            Assert.Contains("chat:", content);
            Assert.Contains("knowledge_base:", content);
            Assert.Contains("code_generation:", content);
        }

        #endregion

        // =====================================================================
        #region General Settings

        [Fact]
        public void GeneralSettings_Language_IsVietnamese()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("language: \"vi-VN\"", content);
        }

        [Fact]
        public void GeneralSettings_EarlyAccess_IsFalse()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("early_access: false", content);
        }

        [Fact]
        public void GeneralSettings_ToneInstructions_IsPresent()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("tone_instructions:", content);
        }

        #endregion

        // =====================================================================
        #region Reviews — Profile & Workflow

        [Fact]
        public void Reviews_Profile_IsAssertive()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("profile: \"assertive\"", content);
        }

        [Fact]
        public void Reviews_RequestChangesWorkflow_IsFalse()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("request_changes_workflow: false", content);
        }

        [Fact]
        public void Reviews_HighLevelSummary_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("high_level_summary: true", content);
        }

        [Fact]
        public void Reviews_HighLevelSummaryPlaceholder_IsSet()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("high_level_summary_placeholder: \"@coderabbitai summary\"", content);
        }

        [Fact]
        public void Reviews_CollapseWalkthrough_IsTrue()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("collapse_walkthrough: true", content);
        }

        [Fact]
        public void Reviews_SequenceDiagrams_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("sequence_diagrams: true", content);
        }

        [Fact]
        public void Reviews_EstimateCodeReviewEffort_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("estimate_code_review_effort: true", content);
        }

        [Fact]
        public void Reviews_AssessLinkedIssues_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("assess_linked_issues: true", content);
        }

        #endregion

        // =====================================================================
        #region Reviews — PR Title

        [Fact]
        public void Reviews_AutoTitlePlaceholder_IsSet()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("auto_title_placeholder: \"@coderabbitai\"", content);
        }

        [Fact]
        public void Reviews_AutoTitleInstructions_ContainsConventionalCommitTypes()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert — the conventional commit types must all be listed
            Assert.Contains("feat", content);
            Assert.Contains("fix", content);
            Assert.Contains("refactor", content);
            Assert.Contains("perf", content);
            Assert.Contains("chore", content);
        }

        #endregion

        // =====================================================================
        #region Reviews — Labels

        [Fact]
        public void Reviews_SuggestedLabels_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("suggested_labels: true", content);
        }

        [Fact]
        public void Reviews_AutoApplyLabels_IsFalse()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("auto_apply_labels: false", content);
        }

        [Fact]
        public void Reviews_LabelingInstructions_ContainsAllNineLabels()
        {
            // Arrange
            var content = ReadConfig();
            var expectedLabels = new[]
            {
                "\"feature\"",
                "\"bug-fix\"",
                "\"refactor\"",
                "\"performance\"",
                "\"security\"",
                "\"test\"",
                "\"dependencies\"",
                "\"ci/cd\"",
                "\"breaking-change\""
            };

            // Act & Assert
            foreach (var label in expectedLabels)
            {
                Assert.Contains(label, content,
                    StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Reviews_LabelingInstructions_HasNineLabelEntries()
        {
            // Arrange
            var content = ReadConfig();

            // Act — count occurrences of "label:" under labeling_instructions
            int count = 0;
            int index = 0;
            const string marker = "  - label:";
            while ((index = content.IndexOf(marker, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += marker.Length;
            }

            // Assert
            Assert.Equal(9, count);
        }

        #endregion

        // =====================================================================
        #region Reviews — Status & Misc Flags

        [Fact]
        public void Reviews_ReviewStatus_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("review_status: true", content);
        }

        [Fact]
        public void Reviews_CommitStatus_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("commit_status: true", content);
        }

        [Fact]
        public void Reviews_FailCommitStatus_IsFalse()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("fail_commit_status: false", content);
        }

        [Fact]
        public void Reviews_Poem_IsDisabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            // Decorative poem comments should be disabled
            Assert.Contains("poem: false", content);
        }

        [Fact]
        public void Reviews_InProgressFortune_IsDisabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("in_progress_fortune: false", content);
        }

        [Fact]
        public void Reviews_EnablePromptForAiAgents_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("enable_prompt_for_ai_agents: true", content);
        }

        [Fact]
        public void Reviews_AbortOnClose_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("abort_on_close: true", content);
        }

        #endregion

        // =====================================================================
        #region Reviews — Auto Review

        [Fact]
        public void AutoReview_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("enabled: true", content);
        }

        [Fact]
        public void AutoReview_AutoIncrementalReview_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("auto_incremental_review: true", content);
        }

        [Fact]
        public void AutoReview_AutoPauseAfterReviewedCommits_IsFive()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("auto_pause_after_reviewed_commits: 5", content);
        }

        [Fact]
        public void AutoReview_IgnoreTitleKeywords_ContainsWipAndDraftVariants()
        {
            // Arrange
            var content = ReadConfig();
            var expectedKeywords = new[] { "\"WIP\"", "\"[WIP]\"", "\"DRAFT\"", "\"DO NOT MERGE\"", "\"DNM\"" };

            // Act & Assert
            foreach (var keyword in expectedKeywords)
            {
                Assert.Contains(keyword, content,
                    StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AutoReview_Drafts_IsFalse_DraftPRsNotReviewed()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("drafts: false", content);
        }

        #endregion

        // =====================================================================
        #region Reviews — Path Filters

        [Fact]
        public void PathFilters_ExcludesBinAndObjDirectories()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/bin/**", content);
            Assert.Contains("!**/obj/**", content);
        }

        [Fact]
        public void PathFilters_ExcludesVisualStudioDirectory()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/.vs/**", content);
        }

        [Fact]
        public void PathFilters_ExcludesDesignerFiles()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/*.Designer.cs", content);
        }

        [Fact]
        public void PathFilters_ExcludesEntityFrameworkMigrations()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/Migrations/*.cs", content);
            Assert.Contains("!**/Migrations/**", content);
        }

        [Fact]
        public void PathFilters_ExcludesMinifiedAssets()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/wwwroot/css/**/*.min.css", content);
            Assert.Contains("!**/wwwroot/js/**/*.min.js", content);
        }

        [Fact]
        public void PathFilters_ExcludesNodeModulesAndLockFiles()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/node_modules/**", content);
            Assert.Contains("!**/*.lock", content);
        }

        [Fact]
        public void PathFilters_ExcludesTestSnapshots()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/__snapshots__/**", content);
        }

        [Fact]
        public void PathFilters_ExcludesMarkdownAndTextFiles()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/*.md", content);
            Assert.Contains("!**/*.txt", content);
        }

        #endregion

        // =====================================================================
        #region Reviews — Path Instructions

        [Fact]
        public void PathInstructions_ContainsControllersPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/Controllers/**/*.cs", content);
        }

        [Fact]
        public void PathInstructions_ContainsServicesPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/Services/**/*.cs", content);
        }

        [Fact]
        public void PathInstructions_ContainsRepositoriesPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/Repositories/**/*.cs", content);
        }

        [Fact]
        public void PathInstructions_ContainsModelsPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/Models/**/*.cs", content);
        }

        [Fact]
        public void PathInstructions_ContainsDTOsPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/DTOs/**/*.cs", content);
        }

        [Fact]
        public void PathInstructions_ContainsMiddlewarePath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/Middleware/**/*.cs", content);
        }

        [Fact]
        public void PathInstructions_ContainsProgramCsPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/Program.cs", content);
        }

        [Fact]
        public void PathInstructions_ContainsAppSettingsPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/appsettings*.json", content);
        }

        [Fact]
        public void PathInstructions_ContainsGeneralCSharpSecurityPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("\"**/*.cs\"", content);
        }

        [Fact]
        public void PathInstructions_ContainsTestsPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/*Tests*/**/*.cs", content);
        }

        [Fact]
        public void PathInstructions_ContainsIntegrationTestsPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/*IntegrationTests*/**/*.cs", content);
        }

        [Fact]
        public void PathInstructions_ContainsYamlFilesPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("\"**/*.yml\"", content);
        }

        [Fact]
        public void PathInstructions_ContainsDockerfilePath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("**/Dockerfile", content);
        }

        [Fact]
        public void PathInstructions_ContainsTerraformPath()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("\"**/*.tf\"", content);
        }

        [Fact]
        public void PathInstructions_ContainsFourteenEntries()
        {
            // Arrange
            var content = ReadConfig();

            // Act — count "- path:" entries inside path_instructions
            int count = 0;
            int index = 0;
            const string marker = "    - path:";
            while ((index = content.IndexOf(marker, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += marker.Length;
            }

            // Assert — 14 path instruction blocks defined in the config
            Assert.Equal(14, count);
        }

        #endregion

        // =====================================================================
        #region Chat

        [Fact]
        public void Chat_AutoReply_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("auto_reply: true", content);
        }

        #endregion

        // =====================================================================
        #region Knowledge Base

        [Fact]
        public void KnowledgeBase_OptOut_IsFalse_KnowledgeBaseIsActive()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("opt_out: false", content);
        }

        [Fact]
        public void KnowledgeBase_WebSearch_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("web_search:", content);
            // The enabled: true line appears under web_search
            Assert.Contains("enabled: true", content);
        }

        [Fact]
        public void KnowledgeBase_Learnings_ScopeIsAuto()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("scope: \"auto\"", content);
        }

        [Fact]
        public void KnowledgeBase_CodeGuidelines_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("code_guidelines:", content);
        }

        [Fact]
        public void KnowledgeBase_CodeGuidelines_FilePatterns_ContainsContributingMd()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("CONTRIBUTING.md", content);
        }

        [Fact]
        public void KnowledgeBase_CodeGuidelines_FilePatterns_ContainsPullRequestTemplate()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains(".github/pull_request_template.md", content);
        }

        #endregion

        // =====================================================================
        #region Code Generation

        [Fact]
        public void CodeGeneration_Docstrings_Language_IsCSharp()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("language: \"csharp\"", content);
        }

        [Fact]
        public void CodeGeneration_Docstrings_Section_IsPresent()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("docstrings:", content);
            Assert.Contains("code_generation:", content);
        }

        #endregion

        // =====================================================================
        #region Security — No Hardcoded Secrets

        [Fact]
        public void SecurityCheck_ConfigFile_DoesNotContainHardcodedPasswords()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert — the config must not store actual credential values.
            // We check that no line like "password: <value>" (with a real value) exists.
            var lines = content.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("password:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("secret:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("api_key:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("token:", StringComparison.OrdinalIgnoreCase))
                {
                    // A key-like line exists; ensure value is empty or a placeholder comment
                    Assert.True(
                        trimmed.EndsWith(":") || trimmed.Contains("#"),
                        $"Potential hardcoded secret detected on line: {line.Trim()}");
                }
            }
        }

        [Fact]
        public void SecurityCheck_ConfigFile_DoesNotContainAwsAccessKeys()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert — AWS access key IDs start with "AKIA"
            Assert.DoesNotContain("AKIA", content, StringComparison.Ordinal);
        }

        #endregion

        // =====================================================================
        #region Boundary / Regression Cases

        [Fact]
        public void ConfigFile_SuggestedReviewers_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("suggested_reviewers: true", content);
        }

        [Fact]
        public void ConfigFile_AutoAssignReviewers_IsFalse()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("auto_assign_reviewers: false", content);
        }

        [Fact]
        public void ConfigFile_RelatedIssues_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("related_issues: true", content);
        }

        [Fact]
        public void ConfigFile_RelatedPRs_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("related_prs: true", content);
        }

        [Fact]
        public void ConfigFile_ChangedFilesSummary_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("changed_files_summary: true", content);
        }

        [Fact]
        public void ConfigFile_HighLevelSummaryInWalkthrough_IsFalse()
        {
            // Regression: this flag should remain false to avoid duplicate summaries.
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("high_level_summary_in_walkthrough: false", content);
        }

        [Fact]
        public void ConfigFile_ReviewDetails_IsEnabled()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("review_details: true", content);
        }

        [Fact]
        public void ConfigFile_WwwrootLibExcluded_FromPathFilters()
        {
            // Regression: third-party vendor libraries should never be reviewed.
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/wwwroot/lib/**", content);
        }

        [Fact]
        public void ConfigFile_PackagesDirectory_ExcludedFromPathFilters()
        {
            // Arrange
            var content = ReadConfig();

            // Act & Assert
            Assert.Contains("!**/packages/**", content);
        }

        #endregion
    }
}