using Microsoft.EntityFrameworkCore;
using TechnicalAnalyzer.Models;

namespace TechnicalAnalyzer.Data
{
    public class OhlcDbContext : DbContext
    {
        public OhlcDbContext(DbContextOptions<OhlcDbContext> options) : base(options) { }
        public DbSet<OhlcData> OhlcDatas { get; set; }
    }
}
