using flavia.Model;
using Microsoft.EntityFrameworkCore;
namespace flavia
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        { }

        public DbSet<ConsumoAgua> ConsumosAgua { get; set; }
    }
}