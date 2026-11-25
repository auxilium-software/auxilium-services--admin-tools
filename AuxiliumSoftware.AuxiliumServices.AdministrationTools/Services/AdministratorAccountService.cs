using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework;
using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.EntityModels;
using AuxiliumSoftware.AuxiliumServices.Common.Enumerators;
using AuxiliumSoftware.AuxiliumServices.Common.Services;
using AuxiliumSoftware.AuxiliumServices.Common.Tenancy;
using AuxiliumSoftware.AuxiliumServices.Common.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.Services;

public sealed record UserAccountDetails
{
    public required string EmailAddress { get; init; }
    public required string FullName { get; init; }
    public string LanguagePreference { get; init; } = "en-GB";
    // public string? TelephoneNumber { get; init; }
    // public string? FullAddress { get; init; }
    // public string? Gender { get; init; }
    // public DateOnly? DateOfBirth { get; init; }
    // public string? HowDidYouFindOutAboutOurService { get; init; }

    public bool IsAdministrator { get; init; }
    public bool IsCaseWorker { get; init; }
    public bool IsCaseWorkerManager { get; init; }

    public DateTime? CreatedAtUtc { get; init; }
}

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

    public async Task<UserEntityModel> CreateUserAsync(
        UserAccountDetails details,
        string rawPassword,
        CancellationToken cancellationToken
    )
    {
        if (!tenantContext.IsResolved)
        {
            throw new InvalidOperationException("A tenant must be pinned before creating a user.");
        }

        var user = new UserEntityModel
        {
            Id = UUIDUtilities.GenerateV5(DatabaseObjectTypeEnum.WithinTenancy_User),
            CreatedAtUtc = details.CreatedAtUtc ?? DateTime.UtcNow,
            CreatedByUserId = null,
            LastUpdatedAtUtc = null,
            LastUpdatedByUserId = null,

            EmailAddress = details.EmailAddress.Trim(),
            PasswordHash = HashPassword(rawPassword),
            FullName = details.FullName,
            /*
            FullAddress = details.FullAddress,
            TelephoneNumber = details.TelephoneNumber,
            Gender = details.Gender,
            DateOfBirth = details.DateOfBirth,
            HowDidYouFindOutAboutOurService = details.HowDidYouFindOutAboutOurService,
            */
            LanguagePreference = details.LanguagePreference,

            IsAdministrator = details.IsAdministrator,
            IsCaseWorker = details.IsCaseWorker,
            IsCaseWorkerManager = details.IsCaseWorkerManager,
            AllowLogin = true,
            MustChangePassword = false,
            HasEmailAddressBeenVerified = true,
            DeletionRequested = false,
            DeletionRequestReason = null,
        };

        dbContext.WithinTenancy_Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<UserEntityModel> CreateAdministratorAsync(
        string emailAddress,
        string fullName,
        string rawPassword,
        CancellationToken cancellationToken
    )
    {
        var user = await CreateUserAsync(
            new UserAccountDetails
            {
                EmailAddress = emailAddress,
                FullName = fullName,
                IsAdministrator = true,
            },
            rawPassword,
            cancellationToken
        );

        await webApplicationFirewall.WhitelistUserAsync(
            user,
            "SYSTEM: Administrator created via the Admin Tools CLI.",
            adminUser: user,
            permanent: true,
            ct: cancellationToken
        );

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
