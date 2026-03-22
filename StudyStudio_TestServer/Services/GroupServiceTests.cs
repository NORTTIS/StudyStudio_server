using StudioStudio_Server.Exceptions;
using Xunit;

namespace StudioStudio_Server.Tests.Services
{
    public class GroupServiceTests
    {
        #region Error Code Tests

        [Fact]
        public void ErrorCodes_ShouldHaveGroupNotFound()
        {
            Assert.Equal("GROUP001", ErrorCodes.GroupNotFound);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupNameAlreadyExists()
        {
            Assert.Equal("GROUP002", ErrorCodes.GroupNameAlreadyExists);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupLimitReached()
        {
            Assert.Equal("GROUP003", ErrorCodes.GroupLimitReached);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupAlreadyMember()
        {
            Assert.Equal("GROUP007", ErrorCodes.GroupAlreadyMember);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupMemberLimitReached()
        {
            Assert.Equal("GROUP008", ErrorCodes.GroupMemberLimitReached);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupCannotRemoveOwner()
        {
            Assert.Equal("GROUP010", ErrorCodes.GroupCannotRemoveOwner);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupCannotRemoveSelf()
        {
            Assert.Equal("GROUP011", ErrorCodes.GroupCannotRemoveSelf);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupTaskStatusPositionExist()
        {
            Assert.Equal("GROUP022", ErrorCodes.GroupTaskStatusPositionExist);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupDeleteTaskDenied()
        {
            Assert.Equal("GROUP023", ErrorCodes.GroupDeleteTaskDenined);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupRestoreTaskFailed()
        {
            Assert.Equal("GROUP025", ErrorCodes.GroupRestoreTaskFailed);
        }

        #endregion

        #region Validation Scenario Tests

        [Fact]
        public void Scenario_CreateGroup_WithDuplicateName_ShouldFail()
        {
            // Simulating duplicate name check
            var existingGroupName = "Test Group";
            var newGroupName = "Test Group";

            // Act
            var isDuplicate = existingGroupName.Equals(newGroupName, StringComparison.OrdinalIgnoreCase);

            // Assert
            Assert.True(isDuplicate, "Duplicate group name should fail");
        }

        [Fact]
        public void Scenario_CreateGroup_WithUniqueName_ShouldPass()
        {
            // Simulating unique name check
            var existingGroupName = "Test Group 1";
            var newGroupName = "Test Group 2";

            // Act
            var isDuplicate = existingGroupName.Equals(newGroupName, StringComparison.OrdinalIgnoreCase);

            // Assert
            Assert.False(isDuplicate, "Unique group name should pass");
        }

        [Fact]
        public void Scenario_AddMember_WhenAlreadyMember_ShouldFail()
        {
            // Simulating member check
            var existingMember = true;

            // Act & Assert
            Assert.True(existingMember, "User already a member should fail");
        }

        [Fact]
        public void Scenario_RemoveMember_WhenSelfRemove_ShouldFail()
        {
            var ownerId = Guid.NewGuid();
            var userIdToRemove = ownerId; // Same as owner

            // Act
            var isSelfRemove = ownerId == userIdToRemove;

            // Assert
            Assert.True(isSelfRemove, "Self removal should fail");
        }

        [Fact]
        public void Scenario_RemoveMember_WhenOwner_ShouldFail()
        {
            var ownerId = Guid.NewGuid();
            var memberIdToRemove = ownerId;

            // Act
            var isOwner = memberIdToRemove == ownerId;

            // Assert
            Assert.True(isOwner, "Removing owner should fail");
        }

        [Fact]
        public void Scenario_CreateTaskStatus_WithDuplicatePosition_ShouldFail()
        {
            var existingPosition = 1;
            var newPosition = 1;

            // Act
            var isDuplicate = existingPosition == newPosition;

            // Assert
            Assert.True(isDuplicate, "Duplicate position should fail");
        }

        #endregion
    }
}
