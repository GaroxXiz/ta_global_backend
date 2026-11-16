using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using TAGlobal.Api.Data;

namespace TAGlobal.Api.Application.Abstractions
{
    public interface IHealthRepository
    {
        Task<bool> CanConnectAsync(CancellationToken cancellationToken);
    }
}

namespace TAGlobal.Api.Infrastructure.Repositories
{
    using TAGlobal.Api.Application.Abstractions;

    public sealed class EfHealthRepository(AppDbContext db) : IHealthRepository
    {
        // Raw SQL: Memeriksa konektivitas database dengan query SELECT 1
        public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
        {
            try
            {
                await db.Database
                    .SqlQuery<int>($"""
                        SELECT 1 AS "Value"
                        """)
                    .SingleAsync(cancellationToken);

                return true;
            }
            catch (DbException)
            {
                return false;
            }
        }
    }
}
