using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.EntityModels;
using AuxiliumSoftware.AuxiliumServices.Common.Enumerators;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Tenancy;
using AuxiliumSoftware.AuxiliumServices.Common.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Services;

public sealed class AdministratorAccountService(
    AuxiliumDbContext dbContext,
    ITenantContext tenantContext,
    IPasswordService passwordService,
    IWebApplicationFirewallService webApplicationFirewall
)
{
    public Task<bool> EmailAddressExistsAsync(string emailAddress, CancellationToken cancellationToken) =>
        dbContext.WithinTenancy_Users.AnyAsync(u => u.EmailAddress == emailAddress, cancellationToken);

    public Task<UserEntityModel?> FindByEmailAddressAsync(string emailAddress, CancellationToken cancellationToken) =>
        dbContext.WithinTenancy_Users.FirstOrDefaultAsync(u => u.EmailAddress == emailAddress, cancellationToken);

    public async Task<UserEntityModel> CreateAdministratorAsync(
        string emailAddress,
        string fullName,
        string rawPassword,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.IsResolved)
        {
            throw new InvalidOperationException("A tenant must be pinned before creating an administrator.");
        }

        var user = new UserEntityModel
        {
            Id = UUIDUtilities.GenerateV5(DatabaseObjectTypeEnum.WithinTenancy_User),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = null,
            LastUpdatedAtUtc = null,
            LastUpdatedByUserId = null,

            EmailAddress = emailAddress,
            PasswordHash = HashPassword(rawPassword),
            FullName = fullName,
            FullAddress = null,
            TelephoneNumber = null,
            Gender = null,
            DateOfBirth = null,
            HowDidYouFindOutAboutOurService = null,
            LanguagePreference = "en-GB",

            IsAdministrator = true,
            IsCaseWorker = false,
            IsCaseWorkerManager = false,
            AllowLogin = true,
            MustChangePassword = false,
            HasEmailAddressBeenVerified = true,
            DeletionRequested = false,
            DeletionRequestReason = null,
        };

        dbContext.WithinTenancy_Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        await webApplicationFirewall.WhitelistUserAsync(
            user,
            "SYSTEM: Administrator created via the Admin Tools CLI.",
            adminUser: user,
            permanent: true,
            ct: cancellationToken);

        return user;
    }

    public void SetPassword(UserEntityModel user, string rawPassword, bool mustChangePassword)
    {
        user.PasswordHash = HashPassword(rawPassword);
        user.MustChangePassword = mustChangePassword;
        user.LastUpdatedAtUtc = DateTime.UtcNow;
        user.LastUpdatedByUserId = null; // changed by a server administrator, not by a user account
    }

    public Task<int> RevokeRefreshTokensAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.WithinTenancy_UserRefreshTokens
            .Where(t => t.CreatedByUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

    private string HashPassword(string rawPassword) =>
        passwordService.HashPassword(passwordService.NormalisePassword(rawPassword, null));
}
