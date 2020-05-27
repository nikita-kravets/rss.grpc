using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace rss.grpc.server.Models
{
    public class RssDataContext : DbContext, IRssManager
    {
        public DbSet<Client> Clients { get; set; }
        public DbSet<Rss> Rsses { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ClientToTag> ClientsToTags { get; set; }
        public DbSet<RssToTag> RssesToTags { get; set; }
        public RssDataContext()
        {
            //Database.EnsureDeleted();
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //var loggerFactory = LoggerFactory.Create(builder => {
            //    builder.AddConsole();
            //});

            optionsBuilder
            //.UseLoggerFactory(loggerFactory) 
            //.EnableSensitiveDataLogging()
            .UseSqlServer(@"Data Source=(localdb)\mssqllocaldb;Database=rss;Trusted_Connection=True;Connection Timeout=30");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RssToTag>(e => e.HasKey(p => new { p.RssFk, p.TagFk }));
            modelBuilder.Entity<ClientToTag>(e => e.HasKey(p => new { p.ClientFk, p.TagFk }));
        }

        public async Task<int> AddClient(string name, string email)
        {
            var found = await Clients.Where(c => c.Email == email).FirstOrDefaultAsync();

            if (found == null)
            {
                Client c = new Client() { Name = name, Email = email };
                Clients.Add(c);

                await SaveChangesAsync();

                return c.ClientId;
            }
            return found.ClientId;
        }

        public async Task<bool> Subscribe(int id, string tag)
        {
            var found = await Clients.Where(c => c.ClientId == id).FirstOrDefaultAsync();

            if (found != null)
            {
                var tg = await Tags.Where(t => t.TagName == tag).FirstOrDefaultAsync();
                if (tg == null)
                {
                    Tag t = new Tag() { TagName = tag };
                    Tags.Add(t);
                    await SaveChangesAsync();
                    ClientToTag ct = new ClientToTag() { TagFk = t.TagId, ClientFk = id };
                    ClientsToTags.Add(ct);
                    await SaveChangesAsync();
                }
                else
                {
                    if (await ClientsToTags
                        .Where(ct => ct.TagFk == tg.TagId && ct.ClientFk == id)
                        .CountAsync() == 0)
                    {
                        ClientToTag ct = new ClientToTag() { TagFk = tg.TagId, ClientFk = id };
                        ClientsToTags.Add(ct);
                        await SaveChangesAsync();
                    }
                }
                return true;
            }

            return false;
        }

        public async Task<bool> Unsubscribe(int id, string tag)
        {
            var found = await Clients.Where(c => c.ClientId == id).FirstOrDefaultAsync();

            if (found != null)
            {
                var tg = await ClientsToTags.Where(t => t.ClientFk == id && t.Tag.TagName == tag).FirstOrDefaultAsync();
                if (tg != null)
                {
                    ClientsToTags.Remove(tg);
                    await SaveChangesAsync();
                }

                return true;
            }

            return false;
        }

        public async Task<Client> GetClient(int id)
        {
            return await Clients.Where(c => c.ClientId == id)
                .Include(c => c.ClientToTags)
                .ThenInclude(t => t.Tag).FirstOrDefaultAsync();
        }

        public async Task<bool> HasRss(string guid)
        {
            return await Rsses.Where(r => r.Guid == guid).CountAsync() > 0;
        }

        public async Task<int> CreateRss(string guid, string xml, DateTime date)
        {
            var rss = await Rsses.Where(r => r.Guid == guid).FirstOrDefaultAsync();

            if (rss == null)
            {
                rss = new Rss() { Guid = guid, FeedXml = xml, PubDate = date };
                Rsses.Add(rss);
                await SaveChangesAsync();
            }
            return rss.RssId;
        }

        public async Task<bool> RssAddTag(int rssId, string tag)
        {
            var rss = await Rsses.Where(r => r.RssId == rssId).FirstOrDefaultAsync();

            if (rss != null)
            {
                var tg = await Tags.Where(t => t.TagName == tag).FirstOrDefaultAsync();
                if (tg == null)
                {
                    tg = new Tag() { TagName = tag };
                    Tags.Add(tg);
                    await SaveChangesAsync();
                    RssToTag rst = new RssToTag() { TagFk = tg.TagId, RssFk = rss.RssId };
                    RssesToTags.Add(rst);
                    await SaveChangesAsync();
                    return true;
                }
                else
                {
                    if (await RssesToTags.Where(r => r.RssFk == rssId && r.TagFk == tg.TagId).CountAsync() == 0)
                    {
                        RssToTag rst = new RssToTag() { TagFk = tg.TagId, RssFk = rss.RssId };
                        RssesToTags.Add(rst);
                        await SaveChangesAsync();
                    }
                }
                return true;
            }
            return false;
        }

        public async Task<List<Rss>> RssForClient(int clientId, int startId, DateTime minDate)
        {
            return await Rsses.Where(r => r.RssId > startId && r.PubDate >= minDate)
                .Where(r => r.RssToTags
                .Where(rt => rt.Tag.ClientToTags
                .Where(ct => ct.ClientFk == clientId)
                .Count() > 0).Count() > 0)
                .Include(r => r.RssToTags)
                .ThenInclude(t => t.Tag)
                .ToListAsync();
        }
    }
}
