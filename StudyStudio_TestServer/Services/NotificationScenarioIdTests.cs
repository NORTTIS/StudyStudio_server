using System.Text.RegularExpressions;

namespace StudioStudio_Server.Tests.Services
{
    public class NotificationScenarioIdTests
    {
        private sealed record ScenarioCase(
            string Id,
            string Trigger,
            string Recipient,
            bool RespectEmailFlag);

        private static readonly List<ScenarioCase> Cases = new()
        {
            new("TC-01", "Task Assignment", "Assignee", true),
            new("TC-02", "Task Assignment (assignee disabled flag)", "Assignee", true),
            new("TC-03", "Task Reassignment", "OldAssignee+NewAssignee", true),
            new("TC-04", "Task Unassigned", "OldAssignee", true),
            new("TC-05", "Task Status Change", "CurrentAssignee", true),
            new("TC-06", "Task Completed", "CurrentAssignee", true),
            new("TC-07", "Task Deleted (SoftDelete)", "Owner+ModeratorOnly", true),
            new("TC-08", "Task Comment Mention", "MentionedUser", true),
            new("TC-09", "Group Discuss Mention", "MentionedUser", true),
            new("TC-10", "Deadline Reminder", "Assignee", true),
            new("TC-11", "Overdue Reminder", "Assignee", true),
            new("TC-12", "Reminder/Overdue Dedup", "Assignee", true),
            new("TC-13", "Payment Success", "PayerMandatory", false),
            new("TC-14", "Payment Failed", "PayerMandatory", false)
        };

        [Fact]
        public void ScenarioCatalog_ShouldContainAllExpectedIds_TC01_To_TC14()
        {
            // Arrange
            var expectedIds = Enumerable.Range(1, 14)
                .Select(i => $"TC-{i:D2}")
                .ToHashSet();

            // Act
            var actualIds = Cases.Select(x => x.Id).ToHashSet();

            // Assert
            Assert.Equal(expectedIds.Count, actualIds.Count);
            Assert.Subset(actualIds, expectedIds);
            Assert.Subset(expectedIds, actualIds);
        }

        [Fact]
        public void ScenarioCatalog_Ids_ShouldBeUnique_AndFollowFormat()
        {
            // Act
            var allIds = Cases.Select(x => x.Id).ToList();
            var uniqueIds = allIds.Distinct().ToList();

            // Assert uniqueness
            Assert.Equal(allIds.Count, uniqueIds.Count);

            // Assert format TC-XX
            foreach (var id in allIds)
            {
                Assert.Matches(new Regex(@"^TC-\d{2}$"), id);
            }
        }

        [Fact]
        public void Scenario_TC07_SoftDelete_ShouldNotifyOwnerAndModeratorOnly()
        {
            // Arrange
            var tc07 = Cases.Single(x => x.Id == "TC-07");

            // Assert
            Assert.Equal("Task Deleted (SoftDelete)", tc07.Trigger);
            Assert.Equal("Owner+ModeratorOnly", tc07.Recipient);
            Assert.True(tc07.RespectEmailFlag);
        }

        [Fact]
        public void Scenario_TC13_TC14_PaymentEmails_ShouldBeMandatory_IgnoreFlag()
        {
            // Arrange
            var tc13 = Cases.Single(x => x.Id == "TC-13");
            var tc14 = Cases.Single(x => x.Id == "TC-14");

            // Assert
            Assert.Equal("PayerMandatory", tc13.Recipient);
            Assert.Equal("PayerMandatory", tc14.Recipient);
            Assert.False(tc13.RespectEmailFlag);
            Assert.False(tc14.RespectEmailFlag);
        }

        [Theory]
        [InlineData("TC-01", "Assignee")]
        [InlineData("TC-03", "OldAssignee+NewAssignee")]
        [InlineData("TC-04", "OldAssignee")]
        [InlineData("TC-05", "CurrentAssignee")]
        [InlineData("TC-06", "CurrentAssignee")]
        [InlineData("TC-08", "MentionedUser")]
        [InlineData("TC-09", "MentionedUser")]
        [InlineData("TC-10", "Assignee")]
        [InlineData("TC-11", "Assignee")]
        [InlineData("TC-12", "Assignee")]
        public void RecipientMatrix_ShouldMatchExpected(string id, string expectedRecipient)
        {
            // Act
            var scenario = Cases.Single(x => x.Id == id);

            // Assert
            Assert.Equal(expectedRecipient, scenario.Recipient);
        }
    }
}
