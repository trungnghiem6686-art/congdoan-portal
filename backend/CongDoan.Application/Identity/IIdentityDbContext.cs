using CongDoan.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace CongDoan.Application.Identity;

public interface IIdentityDbContext
{
    DbSet<AdminUser> AdminUsers { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<Session> Sessions { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string hash, string password);
}
