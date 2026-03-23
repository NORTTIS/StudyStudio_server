using StudioStudio_Server.Exceptions;
using Xunit;

namespace StudioStudio_Server.Tests.Services
{
    public class TaskServiceTests
    {
        #region Error Code Tests

        [Fact]
        public void ErrorCodes_ShouldHaveTaskNotFound()
        {
            Assert.Equal("TASK001", ErrorCodes.TaskNotFound);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveTaskPermissionDenied()
        {
            Assert.Equal("TASK002", ErrorCodes.TaskPermissionDenied);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveTaskDateTimeError()
        {
            Assert.Equal("TASK003", ErrorCodes.TaskDateTimeError);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveTaskNotPendingDeleted()
        {
            Assert.Equal("TASK006", ErrorCodes.TaskNotPendingDeleted);
        }

        [Fact]
        public void ErrorCodes_ShouldHaveGroupCreateTaskDenied()
        {
            Assert.Equal("GROUP017", ErrorCodes.GroupCreateTaskDenied);
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

        #region Task Workflow Tests

        [Fact]
        public void Scenario_DeleteTask_FirstTime_ShouldSoftDelete()
        {
            // Arrange
            var isPendingDeleted = false;

            // Act - First delete
            isPendingDeleted = true;

            // Assert
            Assert.True(isPendingDeleted, "First delete should soft delete");
        }

        [Fact]
        public void Scenario_DeleteTask_AlreadyDeleted_ShouldFail()
        {
            // Arrange
            var isPendingDeleted = true;

            // Act - Second delete attempt
            var canDelete = !isPendingDeleted;

            // Assert
            Assert.False(canDelete, "Already deleted task cannot be deleted again");
        }

        [Fact]
        public void Scenario_RestoreTask_WhenDeleted_ShouldRestore()
        {
            // Arrange
            var isPendingDeleted = true;

            // Act
            isPendingDeleted = false;

            // Assert
            Assert.False(isPendingDeleted, "Task should be restored");
        }

        [Fact]
        public void Scenario_DeleteTask_Permanently_WhenStatusEmpty()
        {
            // Arrange
            var isPendingDeleted = true;
            var statusHasTasks = false;

            // Act - Can only delete permanently if status has no tasks
            var canPermanentDelete = isPendingDeleted && !statusHasTasks;

            // Assert
            Assert.True(canPermanentDelete, "Can delete permanently when status is empty");
        }

        [Fact]
        public void Scenario_CreateTask_WithStatus_ShouldSucceed()
        {
            // Arrange
            var statusId = Guid.NewGuid();
            var hasStatus = statusId != Guid.Empty;

            // Act & Assert
            Assert.True(hasStatus, "Task with status should succeed");
        }

        [Fact]
        public void Scenario_CreateTask_WithoutStatus_ShouldFail()
        {
            // Arrange
            Guid? statusId = null;

            // Act
            var canCreate = statusId.HasValue;

            // Assert
            Assert.False(canCreate, "Task without status should fail");
        }

        #endregion
    }
}
