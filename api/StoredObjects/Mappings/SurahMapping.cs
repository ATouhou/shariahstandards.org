using System.Data.Entity;
using System.Data.Entity.ModelConfiguration;

namespace StoredObjects.Mappings
{
    public class SurahMapping : IMapping
    {
        private DbModelBuilder _modelBuilder;

        private EntityTypeConfiguration<Surah> E => _modelBuilder.Entity<Surah>();

        public void SetMapping(DbModelBuilder modelBuilder)
        {
            _modelBuilder = modelBuilder;
            E.HasKey(x => x.SurahNumber);
            E.Property(x => x.ArabicName).IsRequired().HasMaxLength(50);
            E.Property(x => x.EnglishName).IsRequired().HasMaxLength(50);
        }
    }
}
