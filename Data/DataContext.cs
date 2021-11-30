using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using docshareqr_link.Entities;
using Microsoft.EntityFrameworkCore;

namespace docshareqr_link.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<DocGroup> DocGroups { get; set; }
    }
}