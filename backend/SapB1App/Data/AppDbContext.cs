using Microsoft.EntityFrameworkCore;
using SapB1App.Models;

namespace SapB1App.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── DbSets ──────────────────────────────────────────────────────────────
    public DbSet<Customer>      Customers      { get; set; }
    public DbSet<Product>       Products       { get; set; }
    public DbSet<Order>         Orders         { get; set; }
    public DbSet<OrderLine>     OrderLines     { get; set; }
    public DbSet<Quote>         Quotes         { get; set; }
    public DbSet<QuoteLine>     QuoteLines     { get; set; }
    public DbSet<DeliveryNote>  DeliveryNotes  { get; set; }
    public DbSet<DeliveryNoteLine> DeliveryNoteLines { get; set; }
    public DbSet<Invoice>       Invoices       { get; set; }
    public DbSet<InvoiceLine>   InvoiceLines   { get; set; }
    public DbSet<CreditNote>    CreditNotes    { get; set; }
    public DbSet<Return>        Returns        { get; set; }
    public DbSet<ReturnLine>    ReturnLines    { get; set; }
    public DbSet<AppUser>       Users          { get; set; }
    public DbSet<Visit>         Visits         { get; set; }
    public DbSet<Payment>       Payments       { get; set; }

    // ── Tracking ────────────────────────────────────────────────────────────
    public DbSet<LocationTrack>    LocationTracks    { get; set; }
    public DbSet<VisitCheckPoint>  VisitCheckPoints  { get; set; }
    public DbSet<DailyTrackSummary> DailyTrackSummaries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Customer (Partenaire) ────────────────────────────────────────────
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.CardCode).HasMaxLength(15).IsRequired();
            e.Property(c => c.CardName).HasMaxLength(100).IsRequired();
            e.Property(c => c.PartnerType).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.ForeignName).HasMaxLength(100);
            e.Property(c => c.GroupCode).HasConversion<string>().HasMaxLength(30).HasDefaultValue(CustomerGroup.Locaux);
            e.Property(c => c.Currency).HasConversion<string>().HasMaxLength(20).HasDefaultValue(CurrencyType.EUR);
            e.Property(c => c.FederalTaxId).HasMaxLength(50);
            e.Property(c => c.Phone).HasMaxLength(20);
            e.Property(c => c.Phone1).HasMaxLength(20);
            e.Property(c => c.Phone2).HasMaxLength(20);
            e.Property(c => c.MobilePhone).HasMaxLength(20);
            e.Property(c => c.Email).HasMaxLength(100);
            e.Property(c => c.Contact).HasMaxLength(100);
            e.Property(c => c.AdditionalIdentificationNumber).HasMaxLength(50);
            e.Property(c => c.UnifiedTaxIdentificationNumber).HasMaxLength(50);
            e.Property(c => c.Location).HasMaxLength(500);
            e.Property(c => c.CreditLimit).HasColumnType("decimal(18,4)");
            e.HasIndex(c => c.CardCode).IsUnique();

            e.HasMany(c => c.Orders)
             .WithOne(o => o.Customer)
             .HasForeignKey(o => o.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(c => c.Quotes)
             .WithOne(q => q.Customer)
             .HasForeignKey(q => q.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(c => c.DeliveryNotes)
             .WithOne(dn => dn.Customer)
             .HasForeignKey(dn => dn.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(c => c.Invoices)
             .WithOne(i => i.Customer)
             .HasForeignKey(i => i.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(c => c.Returns)
             .WithOne(r => r.Customer)
             .HasForeignKey(r => r.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Product ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.ItemCode).HasMaxLength(20).IsRequired();
            e.Property(p => p.ItemName).HasMaxLength(100).IsRequired();
            e.Property(p => p.Price).HasColumnType("decimal(18,4)");
            e.HasIndex(p => p.ItemCode).IsUnique();
        });

        // ── Order ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.DocNum).HasMaxLength(30).IsRequired();
            e.Property(o => o.CardCode).HasMaxLength(15).IsRequired();
            e.Property(o => o.DocTotal).HasColumnType("decimal(18,4)");
            e.Property(o => o.VatTotal).HasColumnType("decimal(18,4)");
            e.Property(o => o.BaseType).HasConversion<string>();
            e.Property(o => o.Currency).HasMaxLength(5).HasDefaultValue("EUR");
            e.Property(o => o.Status).HasConversion<string>();

            e.HasMany(o => o.Lines)
             .WithOne(l => l.Order)
             .HasForeignKey(l => l.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Quote ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Quote>(e =>
        {
            e.HasKey(q => q.Id);
            e.Property(q => q.DocNum).HasMaxLength(30).IsRequired();
            e.Property(q => q.CardCode).HasMaxLength(15).IsRequired();
            e.Property(q => q.DocTotal).HasColumnType("decimal(18,4)");
            e.Property(q => q.VatTotal).HasColumnType("decimal(18,4)");
            e.Property(q => q.BaseType).HasConversion<string>();
            e.Property(q => q.Currency).HasMaxLength(5).HasDefaultValue("EUR");
            e.Property(q => q.Status).HasConversion<string>();

            e.HasMany(q => q.Lines)
             .WithOne(l => l.Quote)
             .HasForeignKey(l => l.QuoteId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuoteLine>(e =>
        {
            e.HasKey(ql => ql.Id);
            e.Property(ql => ql.ItemCode).HasMaxLength(20).IsRequired();
            e.Property(ql => ql.Price).HasColumnType("decimal(18,4)");
            e.Property(ql => ql.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(ql => ql.LineTotal).HasColumnType("decimal(18,4)");
            e.Property(ql => ql.VatPct).HasColumnType("decimal(5,2)");

            e.HasOne(ql => ql.Product)
             .WithMany()
             .HasForeignKey(ql => ql.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── DeliveryNote ───────────────────────────────────────────────────
        modelBuilder.Entity<DeliveryNote>(e =>
        {
            e.HasKey(dn => dn.Id);
            e.Property(dn => dn.DocNum).HasMaxLength(30).IsRequired();
            e.Property(dn => dn.CardCode).HasMaxLength(15).IsRequired();
            e.Property(dn => dn.DocTotal).HasColumnType("decimal(18,4)");
            e.Property(dn => dn.VatTotal).HasColumnType("decimal(18,4)");
            e.Property(dn => dn.BaseType).HasConversion<string>();
            e.Property(dn => dn.Status).HasConversion<string>();

            e.HasMany(dn => dn.Lines)
             .WithOne(l => l.DeliveryNote)
             .HasForeignKey(l => l.DeliveryNoteId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(dn => dn.Order)
             .WithMany()
             .HasForeignKey(dn => dn.OrderId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeliveryNoteLine>(e =>
        {
            e.HasKey(dnl => dnl.Id);
            e.Property(dnl => dnl.ItemCode).HasMaxLength(20).IsRequired();
            e.Property(dnl => dnl.Price).HasColumnType("decimal(18,4)");
            e.Property(dnl => dnl.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(dnl => dnl.LineTotal).HasColumnType("decimal(18,4)");
            e.Property(dnl => dnl.VatPct).HasColumnType("decimal(5,2)");

            e.HasOne(dnl => dnl.Product)
             .WithMany()
             .HasForeignKey(dnl => dnl.ProductId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(dnl => dnl.OrderLine)
             .WithMany()
             .HasForeignKey(dnl => dnl.OrderLineId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Invoice ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Invoice>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.DocNum).HasMaxLength(30).IsRequired();
            e.Property(i => i.CardCode).HasMaxLength(15).IsRequired();
            e.Property(i => i.DocTotal).HasColumnType("decimal(18,4)");
            e.Property(i => i.VatTotal).HasColumnType("decimal(18,4)");
            e.Property(i => i.BaseType).HasConversion<string>();
            e.Property(i => i.Currency).HasMaxLength(5).HasDefaultValue("EUR");
            e.Property(i => i.Status).HasConversion<string>();

            e.HasMany(i => i.Lines)
             .WithOne(l => l.Invoice)
             .HasForeignKey(l => l.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(i => i.DeliveryNote)
             .WithMany()
             .HasForeignKey(i => i.DeliveryNoteId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InvoiceLine>(e =>
        {
            e.HasKey(il => il.Id);
            e.Property(il => il.ItemCode).HasMaxLength(20).IsRequired();
            e.Property(il => il.Price).HasColumnType("decimal(18,4)");
            e.Property(il => il.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(il => il.LineTotal).HasColumnType("decimal(18,4)");
            e.Property(il => il.VatPct).HasColumnType("decimal(5,2)");

            e.HasOne(il => il.Product)
             .WithMany()
             .HasForeignKey(il => il.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CreditNote ──────────────────────────────────────────────────────
        modelBuilder.Entity<CreditNote>(e =>
        {
            e.HasKey(cn => cn.Id);
            e.Property(cn => cn.DocNum).HasMaxLength(30).IsRequired();
            e.Property(cn => cn.Amount).HasColumnType("decimal(18,4)");

            e.HasOne(cn => cn.Invoice)
             .WithMany(i => i.CreditNotes)
             .HasForeignKey(cn => cn.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(cn => cn.Return)
             .WithOne(r => r.CreditNote)
             .HasForeignKey<Return>(r => r.CreditNoteId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Return ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Return>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.ReturnNumber).HasMaxLength(30).IsRequired();
            e.Property(r => r.Status).HasConversion<string>();

            e.HasMany(r => r.Lines)
             .WithOne(rl => rl.Return)
             .HasForeignKey(rl => rl.ReturnId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.DeliveryNote)
             .WithMany()
             .HasForeignKey(r => r.DeliveryNoteId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnLine>(e =>
        {
            e.HasKey(rl => rl.Id);
            e.Property(rl => rl.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(rl => rl.LineTotal).HasColumnType("decimal(18,4)");
            e.Property(rl => rl.VatPct).HasColumnType("decimal(5,2)");

            e.HasOne(rl => rl.Product)
             .WithMany()
             .HasForeignKey(rl => rl.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── OrderLine ────────────────────────────────────────────────────────
        modelBuilder.Entity<OrderLine>(e =>
        {
            e.HasKey(ol => ol.Id);
            e.Property(ol => ol.ItemCode).HasMaxLength(20).IsRequired();
            e.Property(ol => ol.Price).HasColumnType("decimal(18,4)");
            e.Property(ol => ol.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(ol => ol.LineTotal).HasColumnType("decimal(18,4)");
            e.Property(ol => ol.VatPct).HasColumnType("decimal(5,2)");

            e.HasOne(ol => ol.Product)
             .WithMany()
             .HasForeignKey(ol => ol.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── AppUser ─────────────────────────────────────────────────────────
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).HasMaxLength(50).IsRequired();
            e.Property(u => u.Email).HasMaxLength(100).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("User");
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
        });

        // ── Visit ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Visit>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Status).HasConversion<string>();
            e.Property(v => v.Latitude).HasColumnType("float");
            e.Property(v => v.Longitude).HasColumnType("float");
            e.Property(v => v.CheckInLatitude).HasColumnType("float");
            e.Property(v => v.CheckInLongitude).HasColumnType("float");
            e.Property(v => v.CheckOutLatitude).HasColumnType("float");
            e.Property(v => v.CheckOutLongitude).HasColumnType("float");
            e.Property(v => v.DistanceKm).HasColumnType("float");

            e.HasOne(v => v.Customer)
             .WithMany()
             .HasForeignKey(v => v.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(v => v.User)
             .WithMany()
             .HasForeignKey(v => v.UserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(v => v.CheckPoints)
             .WithOne(cp => cp.Visit)
             .HasForeignKey(cp => cp.VisitId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Payment ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Amount).HasColumnType("decimal(18,4)");
            e.Property(p => p.PaymentMethod).HasConversion<string>();
            e.Property(p => p.Reference).HasMaxLength(50);

            e.HasOne(p => p.Customer)
             .WithMany()
             .HasForeignKey(p => p.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Order)
             .WithMany()
             .HasForeignKey(p => p.OrderId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(p => p.Invoice)
             .WithMany(i => i.Payments)
             .HasForeignKey(p => p.InvoiceId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── LocationTrack ────────────────────────────────────────────────────
        modelBuilder.Entity<LocationTrack>(e =>
        {
            e.HasKey(lt => lt.Id);
            e.Property(lt => lt.Latitude).HasColumnType("float").IsRequired();
            e.Property(lt => lt.Longitude).HasColumnType("float").IsRequired();
            e.Property(lt => lt.Accuracy).HasColumnType("float");
            e.Property(lt => lt.Speed).HasColumnType("float");
            e.Property(lt => lt.Heading).HasColumnType("float");
            e.Property(lt => lt.Altitude).HasColumnType("float");

            e.HasIndex(lt => new { lt.UserId, lt.RecordedAt });

            e.HasOne(lt => lt.User)
             .WithMany()
             .HasForeignKey(lt => lt.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── VisitCheckPoint ──────────────────────────────────────────────────
        modelBuilder.Entity<VisitCheckPoint>(e =>
        {
            e.HasKey(vcp => vcp.Id);
            e.Property(vcp => vcp.Type).HasConversion<string>();
            e.Property(vcp => vcp.Latitude).HasColumnType("float").IsRequired();
            e.Property(vcp => vcp.Longitude).HasColumnType("float").IsRequired();
            e.Property(vcp => vcp.Accuracy).HasColumnType("float");
            e.Property(vcp => vcp.Address).HasMaxLength(500);
            e.Property(vcp => vcp.Notes).HasMaxLength(1000);

            e.HasOne(vcp => vcp.User)
             .WithMany()
             .HasForeignKey(vcp => vcp.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── DailyTrackSummary ────────────────────────────────────────────────
        modelBuilder.Entity<DailyTrackSummary>(e =>
        {
            e.HasKey(dts => dts.Id);
            e.Property(dts => dts.TotalDistanceKm).HasColumnType("float");

            e.HasIndex(dts => new { dts.UserId, dts.Date }).IsUnique();

            e.HasOne(dts => dts.User)
             .WithMany()
             .HasForeignKey(dts => dts.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
