using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Models.Entities;

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
        public DbSet<TaskHistory> TaskHistories => Set<TaskHistory>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<GroupAttachment> GroupAttachments => Set<GroupAttachment>();
        public DbSet<PersonalAttachment> PersonalAttachments => Set<PersonalAttachment>();
        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<AIRequestLog> AIRequestLogs => Set<AIRequestLog>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
        public DbSet<Report> Reports => Set<Report>();
        public DbSet<RefreshToken> RefreshToken => Set<RefreshToken>();
        public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // USER
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(x => x.UserId);

                e.HasIndex(x => x.Email).IsUnique();

                e.Property(x => x.Email).IsRequired();
                e.Property(x => x.PasswordHash).IsRequired();
                e.Property(x => x.FullName).IsRequired();
                e.HasOne(u => u.RefreshToken)
                    .WithOne(r => r.User)
                    .HasForeignKey<RefreshToken>(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            //Refresh Token
            modelBuilder.Entity<RefreshToken>(e =>
            {
                e.HasKey(x => x.Id);
            });

            //Email verify token
            modelBuilder.Entity<EmailVerificationToken>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(r => r.UserId)
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

            // TASK HISTORY
            modelBuilder.Entity<TaskHistory>(e =>
            {
                e.HasKey(x => x.HistoryId);
            });

            // COMMENT
            modelBuilder.Entity<Comment>(e =>
            {
                e.HasKey(x => x.CommentId);

                e.Property(x => x.Content).IsRequired();
            });

            // GROUP ATTACHMENT
            modelBuilder.Entity<GroupAttachment>(e =>
            {
                e.HasKey(x => x.GroupAttachmentId);

                e.Property(x => x.FileName).IsRequired();
                e.Property(x => x.FileUrl).IsRequired();
            });

            // PERSONAL ATTACHMENT
            modelBuilder.Entity<PersonalAttachment>(e =>
            {
                e.HasKey(x => x.AttachmentId);

                e.Property(x => x.FileName).IsRequired();
                e.Property(x => x.FileUrl).IsRequired();
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

                e.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(x => x.UserId);

                e.HasOne<SubscriptionPlan>()
                    .WithMany()
                    .HasForeignKey(x => x.PlanId);
            });

            // PAYMENT
            modelBuilder.Entity<Payment>(e =>
            {
                e.HasKey(x => x.PaymentId);
            });

            // AI REQUEST LOG
            modelBuilder.Entity<AIRequestLog>(e =>
            {
                e.HasKey(x => x.RequestId);
            });

            // ACTIVITY LOG
            modelBuilder.Entity<ActivityLog>(e =>
            {
                e.HasKey(x => x.LogId);
            });

            // REPORT
            modelBuilder.Entity<Report>(e =>
            {
                e.HasKey(x => x.ReportId);
            });
        }
    }
}
