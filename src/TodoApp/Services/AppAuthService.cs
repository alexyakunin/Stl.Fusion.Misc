using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Stl.Async;
using Stl.CommandR;
using Stl.CommandR.Commands;
using Stl.DependencyInjection;
using Stl.Fusion;
using Stl.Fusion.Authentication;
using Stl.Fusion.Authentication.Commands;
using Stl.Fusion.Authentication.Internal;
using Stl.Fusion.EntityFramework;
using Stl.Fusion.Operations;
using Stl.Serialization;

namespace TodoApp.Services
{
    [ComputeService]
    [ServiceAlias(typeof(IServerSideAuthService), typeof(AppAuthService))]
    public class AppAuthService : DbServiceBase<AppDbContext>, IServerSideAuthService
    {
        public AppAuthService(IServiceProvider services) : base(services) { }

        // Commands

        public async Task SignInAsync(SignInCommand command, CancellationToken cancellationToken = default)
        {
            var (user, session) = command;
            if (Computed.IsInvalidating()) {
                GetUserAsync(session, default).Ignore();
                GetUserSessionsAsync(user.Id, default).Ignore();
                return;
            }

            if (await IsSignOutForcedAsync(session, cancellationToken).ConfigureAwait(false))
                throw Errors.ForcedSignOut();

            await using var dbContext = await CreateCommandDbContextAsync(cancellationToken).ConfigureAwait(false);
            var dbUser = await GetOrCreateUserAsync(dbContext, user, cancellationToken).ConfigureAwait(false);
            var dbSession = await GetOrCreateSessionAsync(dbContext, session, cancellationToken).ConfigureAwait(false);
            if (dbSession.UserId != dbUser.Id)
                dbSession.UserId = dbUser.Id;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task SignOutAsync(SignOutCommand command, CancellationToken cancellationToken = default)
        {
            var (force, session) = command;
            var context = CommandContext.GetCurrent();
            string? userId;
            if (Computed.IsInvalidating()) {
                GetUserAsync(session, default).Ignore();
                userId = context.Items.TryGet<OperationItem<string?>>()?.Value;
                if (userId != null)
                    GetUserSessionsAsync(userId, default).Ignore();
                if (force)
                    IsSignOutForcedAsync(session, default).Ignore();
                return;
            }

            await using var dbContext = await CreateCommandDbContextAsync(cancellationToken).ConfigureAwait(false);
            var dbSession = await GetOrCreateSessionAsync(dbContext, session, cancellationToken).ConfigureAwait(false);
            userId = dbSession.UserId;
            dbSession.IsSignOutForced = force;
            dbSession.UserId = null;
            context.Items.Set(OperationItem.New(userId));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveSessionInfoAsync(SaveSessionInfoCommand command, CancellationToken cancellationToken = default)
        {
            var (sessionInfo, session) = command;
            if (Computed.IsInvalidating()) {
                GetSessionInfoAsync(session, default).Ignore();
                return;
            }

            if (sessionInfo.Id != session.Id)
                throw new ArgumentOutOfRangeException(nameof(sessionInfo));
            var now = Clock.Now.ToDateTime();
            sessionInfo = sessionInfo with { LastSeenAt = now };

            await using var dbContext = await CreateCommandDbContextAsync(cancellationToken).ConfigureAwait(false);
            var dbSession = await GetOrCreateSessionAsync(dbContext, session, cancellationToken).ConfigureAwait(false);
            dbSession.LastSeenAt = sessionInfo.LastSeenAt;
            dbSession.IPAddress = sessionInfo.IPAddress;
            dbSession.UserAgent = sessionInfo.UserAgent;
            dbSession.ExtraPropertiesJson = ToJson(sessionInfo.ExtraProperties!.ToDictionary(kv => kv.Key, kv => kv.Value));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdatePresenceAsync(Session session, CancellationToken cancellationToken = default)
        {
            var sessionInfo = await GetSessionInfoAsync(session, cancellationToken).ConfigureAwait(false);
            var now = Clock.Now.ToDateTime();
            var delta = now - sessionInfo.LastSeenAt;
            if (delta < TimeSpan.FromMinutes(3))
                return; // We don't want to update this too frequently
            sessionInfo = sessionInfo with { LastSeenAt = now };
            var command = new SaveSessionInfoCommand(sessionInfo, session).MarkServerSide();
            await SaveSessionInfoAsync(command, cancellationToken).ConfigureAwait(false);
        }

        // Compute methods

        public virtual async Task<bool> IsSignOutForcedAsync(Session session, CancellationToken cancellationToken = default)
        {
            await using var dbContext = CreateDbContext();
            var dbSession = await dbContext.Sessions.FindAsync(session.Id).ConfigureAwait(false);
            return dbSession?.IsSignOutForced == true;
        }

        public virtual async Task<User> GetUserAsync(Session session, CancellationToken cancellationToken = default)
        {
            if (await IsSignOutForcedAsync(session, cancellationToken).ConfigureAwait(false))
                return new User(session.Id);

            await using var dbContext = CreateDbContext();
            await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var dbSession = await dbContext.Sessions.FindAsync(session.Id).ConfigureAwait(false);
            if (dbSession?.UserId == null || dbSession.IsSignOutForced)
                return new User(session.Id);

            var dbUser = await AsyncEnumerable.SingleAsync(dbContext.Users, u => u.Id == dbSession.UserId, cancellationToken).ConfigureAwait(false);
            return ToUser(dbUser);
        }

        public virtual async Task<SessionInfo> GetSessionInfoAsync(
            Session session, CancellationToken cancellationToken = default)
        {
            await using var dbContext = CreateDbContext();
            await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var now = Clock.Now.ToDateTime();
            var dbSession = await dbContext.Sessions.FindAsync(session.Id).ConfigureAwait(false);
            if (dbSession == null)
                return new SessionInfo(session.Id) {
                    CreatedAt = now,
                    LastSeenAt = now,
                };
            return ToSessionInfo(dbSession);
        }

        public virtual async Task<SessionInfo[]> GetUserSessionsAsync(
            Session session, CancellationToken cancellationToken = default)
        {
            var user = await GetUserAsync(session, cancellationToken).ConfigureAwait(false);
            if (!user.IsAuthenticated)
                return Array.Empty<SessionInfo>();

            return await GetUserSessionsAsync(user.Id, cancellationToken).ConfigureAwait(false);
        }

        [ComputeMethod]
        protected virtual async Task<SessionInfo[]> GetUserSessionsAsync(
            string userId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = CreateDbContext();
            await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var dbSessions = await dbContext.Sessions.AsQueryable()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.LastSeenAt)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            return dbSessions.Select(ToSessionInfo).ToArray();
        }

        // Private methods

        private async Task<DbUser> GetOrCreateUserAsync(AppDbContext dbContext, User user, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(user.AuthenticationType))
                throw new ArgumentOutOfRangeException(nameof(user), "Can't create unauthenticated user.");

            var dbUser = await dbContext.Users.FindAsync(user.Id).ConfigureAwait(false);
            if (dbUser == null) {
                dbUser = new DbUser() {
                    Id = user.Id,
                    AuthenticationType = user.AuthenticationType,
                    Name = user.Name,
                    ClaimsJson = ToJson(user.Claims.ToDictionary(kv => kv.Key, kv => kv.Value)),
                };
                await dbContext.Users.AddAsync(dbUser, cancellationToken);
            }
            return dbUser;
        }

        private async Task<DbSession> GetOrCreateSessionAsync(AppDbContext dbContext, Session session, CancellationToken cancellationToken)
        {
            var dbSession = await dbContext.Sessions.FindAsync(session.Id).ConfigureAwait(false);
            if (dbSession == null) {
                var now = Clock.Now.ToDateTime();
                dbSession = new DbSession() {
                    Id = session.Id,
                    CreatedAt = now,
                    LastSeenAt = now,
                };
                await dbContext.Sessions.AddAsync(dbSession, cancellationToken);
            }
            return dbSession;
        }

        private User ToUser(DbUser dbUser)
            => new(
                dbUser.AuthenticationType, dbUser.Id, dbUser.Name,
                new ReadOnlyDictionary<string, string>(
                    FromJson<Dictionary<string, string>>(dbUser.ClaimsJson) ?? new()));

        private SessionInfo ToSessionInfo(DbSession dbSession)
            => new() {
                Id = dbSession.Id,
                CreatedAt = dbSession.CreatedAt,
                LastSeenAt = dbSession.LastSeenAt,
                IPAddress = dbSession.IPAddress,
                UserAgent = dbSession.UserAgent,
                ExtraProperties = new ReadOnlyDictionary<string, object>(
                    FromJson<Dictionary<string, object>>(dbSession.ExtraPropertiesJson) ?? new()),
            };

        private string ToJson<T>(T source) => JsonSerialized.New(source).SerializedValue;
        private T? FromJson<T>(string json) => JsonSerialized.New<T>(json).Value;
    }
}
