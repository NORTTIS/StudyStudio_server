using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Data
{
    public class StudioDbContext : DbContext
    {
        public StudioDbContext(DbContextOptions<StudioDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Studio> Studios => Set<Studio>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupParticipant> GroupParticipants => Set<GroupParticipant>();
        public DbSet<Favourite> Favourites => Set<Favourite>();
        public DbSet<TaskItem> Tasks => Set<TaskItem>();
        public DbSet<GroupTaskStatus> GroupTaskStatuses => Set<GroupTaskStatus>();
        public DbSet<PersonalTaskStatus> PersonalTaskStatuses => Set<PersonalTaskStatus>();
        public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
        public DbSet<GroupAttachment> GroupAttachments => Set<GroupAttachment>();
        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<AIRequestLog> AIRequestLogs => Set<AIRequestLog>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
        public DbSet<Report> Reports => Set<Report>();
        public DbSet<RefreshToken> RefreshToken => Set<RefreshToken>();
        public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
        public DbSet<Announcement> Announcements => Set<Announcement>();
        public DbSet<Template> Templates => Set<Template>();
        public DbSet<GroupMessage> GroupMessages => Set<GroupMessage>();
        public DbSet<TaskComment> TaskComments => Set<TaskComment>();
        public DbSet<UserAnnouncement> UserAnnouncements => Set<UserAnnouncement>();
        public DbSet<StudioParticipant> StudioParticipants => Set<StudioParticipant>();

        // Analytics Entities
        public DbSet<UserActivityMetrics> UserActivityMetrics => Set<UserActivityMetrics>();
        public DbSet<UserProductivityScores> UserProductivityScores => Set<UserProductivityScores>();
        public DbSet<GroupAnalytics> GroupAnalytics => Set<GroupAnalytics>();
        public DbSet<StudioAnalytics> StudioAnalytics => Set<StudioAnalytics>();
        public DbSet<TaskPerformanceMetrics> TaskPerformanceMetrics => Set<TaskPerformanceMetrics>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // USER
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(x => x.UserId);

                // Email is unique only for non-deleted users (Status != Deleted)
                // This allows new users to register with an email that belonged to a deleted account
                e.HasIndex(x => x.Email)
                    .IsUnique()
                    .HasFilter(@"""Status"" != 2");

                e.Property(x => x.Email).IsRequired();
                e.Property(x => x.PasswordHash).IsRequired();
                e.Property(x => x.FirstName).IsRequired();
                e.Property(x => x.LastName).IsRequired();
            });

            //Refresh Token
            modelBuilder.Entity<RefreshToken>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasOne(r => r.User)
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => x.UserId);
                e.HasIndex(x => x.Token);
            });

            //Email verify token
            modelBuilder.Entity<EmailVerificationToken>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasOne(x => x.User)
                    .WithMany(u => u.EmailVerificationToken)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // STUDIO
            modelBuilder.Entity<Studio>(e =>
            {
                e.HasKey(x => x.StudioId);

                e.Property(x => x.StudioName).IsRequired();

                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(x => x.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasMany(s => s.Participants)
                    .WithOne(p => p.Studio)
                    .HasForeignKey(p => p.StudioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // STUDIO PARTICIPANT
            modelBuilder.Entity<StudioParticipant>(e =>
            {
                e.HasKey(x => x.ParticipantId);

                e.HasIndex(x => new { x.StudioId, x.UserId })
                    .IsUnique();

                e.HasOne<User>(x => x.User)
                    .WithMany(x => x.StudioParticipants)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.Property(x => x.Role)
                    .HasConversion<int>();
            });

            // GROUP
            modelBuilder.Entity<Group>(e =>
            {
                e.HasKey(x => x.GroupId);

                e.Property(x => x.GroupName).IsRequired();

                e.HasOne<Studio>()
                    .WithMany(s => s.Groups)
                    .HasForeignKey(x => x.StudioId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => new { x.StudioId, x.GroupName });
            });

            // GROUP PARTICIPANT
            modelBuilder.Entity<GroupParticipant>(e =>
            {
                e.HasKey(x => x.ParticipantId);

                e.HasIndex(x => new { x.GroupId, x.UserId })
                    .IsUnique(); // BR-16

                e.HasOne<Group>()
                    .WithMany(g => g.Participants)
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne<User>()
                    .WithMany(u => u.GroupParticipants)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // GROUP TASK STATUS
            modelBuilder.Entity<GroupTaskStatus>(e =>
            {
                e.HasKey(x => x.StatusId);

                e.Property(x => x.StatusName).IsRequired();

                e.HasIndex(x => new { x.GroupId, x.Position })
                    .IsUnique();
            });

            // PERSONAL TASK STATUS
            modelBuilder.Entity<PersonalTaskStatus>(e =>
            {
                e.HasKey(x => x.StatusId);

                e.Property(x => x.StatusName).IsRequired();

                e.HasIndex(x => new { x.UserId, x.Position })
                    .IsUnique();

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TASK
            modelBuilder.Entity<TaskItem>(e =>
            {
                e.HasKey(x => x.TaskId);

                e.Property(x => x.Title).IsRequired();

                // optional group (personal task)
                e.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired(false);

                // owner (always required)
                e.HasOne(x => x.Owner)
                    .WithMany()
                    .HasForeignKey(x => x.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);

                // group status (group task)
                e.HasOne(x => x.GroupStatus)
                    .WithMany()
                    .HasForeignKey(x => x.GroupStatusId)
                    .IsRequired(false);

                // personal status (personal task)
                e.HasOne(x => x.PersonalStatus)
                    .WithMany()
                    .HasForeignKey(x => x.PersonalStatusId)
                    .IsRequired(false);

                e.HasIndex(x => x.OwnerId);
                e.HasIndex(x => x.GroupId);
            });

            // TASK ASSIGNMENT
            modelBuilder.Entity<TaskAssignment>(e =>
            {
                e.HasKey(x => x.AssignmentId);

                e.HasIndex(x => new { x.TaskId, x.AssignedTo })
                    .IsUnique();
            });

            // GROUP ATTACHMENT
            modelBuilder.Entity<GroupAttachment>(e =>
            {
                e.HasKey(x => x.GroupAttachmentId);

                e.Property(x => x.FileName).IsRequired();
                e.Property(x => x.FileUrl).IsRequired();

                e.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Uploader)
                    .WithMany()
                    .HasForeignKey(x => x.UploadedBy)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // SUBSCRIPTION PLAN
            modelBuilder.Entity<SubscriptionPlan>(e =>
            {
                e.HasKey(x => x.PlanId);

                e.Property(x => x.PlanName).IsRequired();
                e.HasIndex(x => x.PlanName).IsUnique();
            });

            // USER SUBSCRIPTION
            modelBuilder.Entity<UserSubscription>(e =>
            {
                e.HasKey(x => x.SubscriptionId);

                e.HasOne(x => x.User)
                    .WithMany(u => u.UserSubscriptions)
                    .HasForeignKey(x => x.UserId);

                e.HasOne(x => x.Plan)
                    .WithMany()
                    .HasForeignKey(x => x.PlanId);
            });

            // PAYMENT
            modelBuilder.Entity<Payment>(e =>
            {
                e.HasKey(x => x.PaymentId);

                e.HasIndex(x => x.OrderCode).IsUnique();

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Plan)
                    .WithMany()
                    .HasForeignKey(x => x.PlanId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // AI REQUEST LOG
            modelBuilder.Entity<AIRequestLog>(e =>
            {
                e.HasKey(x => x.RequestId);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ACTIVITY LOG
            modelBuilder.Entity<ActivityLog>(e =>
            {
                e.HasKey(x => x.LogId);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Studio)
                    .WithMany()
                    .HasForeignKey(x => x.StudioId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // REPORT
            modelBuilder.Entity<Report>(e =>
            {
                e.HasKey(x => x.ReportId);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ANNOUNCEMENT
            modelBuilder.Entity<Announcement>(e =>
            {
                e.HasKey(x => x.AnnouncementId);
                e.Property(x => x.Title).IsRequired();
                e.Property(x => x.Content).IsRequired();
                e.HasIndex(x => x.IsActive);
                e.HasIndex(x => x.PublishedAt);
            });

            // TEMPLATE
            modelBuilder.Entity<Template>(e =>
            {
                e.HasKey(x => x.TemplateId);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => x.GroupId).IsUnique();
                e.HasIndex(x => x.IsActive);
            });

            // GROUP MESSAGE
            modelBuilder.Entity<GroupMessage>(e =>
            {
                e.HasKey(x => x.MessageId);

                e.Property(x => x.Content).IsRequired();

                e.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ⚠️ FIX: Changed from Cascade to Restrict to avoid multiple cascade paths
                e.HasOne(x => x.ParentMessage)
                    .WithMany(x => x.Replies)
                    .HasForeignKey(x => x.ParentMessageId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                e.HasIndex(x => x.GroupId);
                e.HasIndex(x => x.CreatedAt);
                e.HasIndex(x => x.ParentMessageId);
            });

            // TASK COMMENT
            modelBuilder.Entity<TaskComment>(e =>
            {
                e.HasKey(x => x.CommentId);

                e.Property(x => x.Content).IsRequired();

                e.HasOne(x => x.Task)
                    .WithMany()
                    .HasForeignKey(x => x.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Changed from Cascade to Restrict to avoid multiple cascade paths
                e.HasOne(x => x.ParentComment)
                    .WithMany(x => x.Replies)
                    .HasForeignKey(x => x.ParentCommentId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired(false);

                e.HasIndex(x => x.TaskId);
                e.HasIndex(x => x.CreatedAt);
                e.HasIndex(x => x.ParentCommentId);
            });

            // USER ANNOUNCEMENT
            modelBuilder.Entity<UserAnnouncement>(e =>
            {
                e.HasKey(x => x.UserAnnouncementId);

                e.HasOne<Announcement>()
                    .WithMany(a => a.UserAnnouncements)
                    .HasForeignKey(x => x.AnnouncementId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(x => x.MentionedId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(x => x.CreatedBy)
                    .OnDelete(DeleteBehavior.SetNull);  // Set null instead of cascade

                e.Property(x => x.IsRead).IsRequired();
                e.Property(x => x.CreatedAt).IsRequired();
            });

            // USER ACTIVITY METRICS
            modelBuilder.Entity<UserActivityMetrics>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasIndex(x => new { x.UserId, x.Date })
                    .IsUnique();

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // USER PRODUCTIVITY SCORES
            modelBuilder.Entity<UserProductivityScores>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasIndex(x => new { x.UserId, x.GroupId, x.WeekStart })
                    .IsUnique();

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired(false);
            });

            // GROUP ANALYTICS
            modelBuilder.Entity<GroupAnalytics>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasIndex(x => new { x.GroupId, x.Date })
                    .IsUnique();

                e.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // STUDIO ANALYTICS
            modelBuilder.Entity<StudioAnalytics>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasIndex(x => new { x.StudioId, x.Date })
                    .IsUnique();

                e.HasOne(x => x.Studio)
                    .WithMany()
                    .HasForeignKey(x => x.StudioId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TASK PERFORMANCE METRICS
            modelBuilder.Entity<TaskPerformanceMetrics>(e =>
            {
                e.HasKey(x => x.Id);

                e.HasIndex(x => x.TaskId)
                    .IsUnique();

                e.HasOne(x => x.Task)
                    .WithMany()
                    .HasForeignKey(x => x.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ACTIVITY LOG - Add indexes for analytics queries
            modelBuilder.Entity<ActivityLog>(e =>
            {
                e.HasKey(x => x.LogId);

                e.HasIndex(x => new { x.UserId, x.CreatedAt });
                e.HasIndex(x => new { x.GroupId, x.CreatedAt });
                e.HasIndex(x => new { x.StudioId, x.CreatedAt });
                e.HasIndex(x => x.ActionType);
            });
        }
    }
}
