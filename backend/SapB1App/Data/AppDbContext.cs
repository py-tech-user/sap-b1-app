using Microsoft.EntityFrameworkCore;
using SapB1App.Models;

namespace SapB1App.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── DbSets ──────────────────────────────────────────────────────────────
    public DbSet<Customer>  Customers  { get; set; }
    public DbSet<Product>   Products   { get; set; }
    public DbSet<Order>     Orders     { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }
    public DbSet<AppUser>   Users      { get; set; }
    public DbSet<Visit>     Visits     { get; set; }
    public DbSet<Payment>   Payments   { get; set; }

    // ── Tracking ────────────────────────────────────────────────────────────
    public DbSet<LocationTrack>    LocationTracks    { get; set; }
    public DbSet<VisitCheckPoint>  VisitCheckPoints  { get; set; }
    public DbSet<DailyTrackSummary> DailyTrackSummaries { get; set; }

    // ── Retours et Réclamations ─────────────────────────────────────────────
    public DbSet<Return>        Returns        { get; set; }
    public DbSet<ReturnLine>    ReturnLines    { get; set; }
    public DbSet<Claim>         Claims         { get; set; }
    public DbSet<ClaimComment>  ClaimComments  { get; set; }
    public DbSet<ServiceTicket> ServiceTickets { get; set; }
    public DbSet<ServicePart>   ServiceParts   { get; set; }

    // ── Cycle Vente/Achat ───────────────────────────────────────────────────
    public DbSet<DeliveryNote>      DeliveryNotes      { get; set; }
    public DbSet<DeliveryNoteLine>  DeliveryNoteLines  { get; set; }
    public DbSet<Supplier>          Suppliers          { get; set; }
    public DbSet<PurchaseOrder>     PurchaseOrders     { get; set; }
    public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; }
    public DbSet<CreditNote>        CreditNotes        { get; set; }
    public DbSet<CreditNoteLine>    CreditNoteLines    { get; set; }
    public DbSet<GoodsReceipt>      GoodsReceipts      { get; set; }
    public DbSet<GoodsReceiptLine>  GoodsReceiptLines  { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Customer ────────────────────────────────────────────────────────
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.CardCode).HasMaxLength(15).IsRequired();
            e.Property(c => c.CardName).HasMaxLength(100).IsRequired();
            e.Property(c => c.Phone).HasMaxLength(20);
            e.Property(c => c.Email).HasMaxLength(100);
            e.Property(c => c.Currency).HasMaxLength(5).HasDefaultValue("EUR");
            e.Property(c => c.CreditLimit).HasColumnType("decimal(18,4)");
            e.HasIndex(c => c.CardCode).IsUnique();

            e.HasMany(c => c.Orders)
             .WithOne(o => o.Customer)
             .HasForeignKey(o => o.CustomerId)
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
            e.Property(o => o.DocTotal).HasColumnType("decimal(18,4)");
            e.Property(o => o.VatTotal).HasColumnType("decimal(18,4)");
            e.Property(o => o.Currency).HasMaxLength(5).HasDefaultValue("EUR");
            e.Property(o => o.Status).HasConversion<string>();

            e.HasMany(o => o.Lines)
             .WithOne(l => l.Order)
             .HasForeignKey(l => l.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── OrderLine ────────────────────────────────────────────────────────
        modelBuilder.Entity<OrderLine>(e =>
        {
            e.HasKey(ol => ol.Id);
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

        // ═══════════════════════════════════════════════════════════════════
        // RETOURS ET RÉCLAMATIONS
        // ═══════════════════════════════════════════════════════════════════

        // ── Return ────────────────────────────────────────────────────────
        modelBuilder.Entity<Return>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.ReturnNumber).HasMaxLength(20).IsRequired();
            e.Property(r => r.Status).HasConversion<string>();
            e.Property(r => r.Reason).HasConversion<string>();
            e.Property(r => r.TotalAmount).HasColumnType("decimal(18,4)");
            e.HasIndex(r => r.ReturnNumber).IsUnique();

            e.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Order).WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.Approver).WithMany().HasForeignKey(r => r.ApprovedBy).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(r => r.Lines).WithOne(l => l.Return).HasForeignKey(l => l.ReturnId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReturnLine>(e =>
        {
            e.HasKey(rl => rl.Id);
            e.Property(rl => rl.Quantity).HasColumnType("decimal(18,4)");
            e.Property(rl => rl.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(rl => rl.LineTotal).HasColumnType("decimal(18,4)");
            e.HasOne(rl => rl.Product).WithMany().HasForeignKey(rl => rl.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Claim ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Claim>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.ClaimNumber).HasMaxLength(20).IsRequired();
            e.Property(c => c.Type).HasConversion<string>();
            e.Property(c => c.Priority).HasConversion<string>();
            e.Property(c => c.Status).HasConversion<string>();
            e.Property(c => c.Subject).HasMaxLength(200).IsRequired();
            e.HasIndex(c => c.ClaimNumber).IsUnique();

            e.HasOne(c => c.Customer).WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Order).WithMany().HasForeignKey(c => c.OrderId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.Product).WithMany().HasForeignKey(c => c.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(c => c.AssignedUser).WithMany().HasForeignKey(c => c.AssignedTo).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(c => c.Comments).WithOne(cc => cc.Claim).HasForeignKey(cc => cc.ClaimId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClaimComment>(e =>
        {
            e.HasKey(cc => cc.Id);
            e.HasOne(cc => cc.User).WithMany().HasForeignKey(cc => cc.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── ServiceTicket ─────────────────────────────────────────────────
        modelBuilder.Entity<ServiceTicket>(e =>
        {
            e.HasKey(st => st.Id);
            e.Property(st => st.TicketNumber).HasMaxLength(20).IsRequired();
            e.Property(st => st.Type).HasConversion<string>();
            e.Property(st => st.Status).HasConversion<string>();
            e.Property(st => st.Priority).HasConversion<string>();
            e.Property(st => st.LaborCost).HasColumnType("decimal(18,4)");
            e.Property(st => st.PartsCost).HasColumnType("decimal(18,4)");
            e.Property(st => st.TotalCost).HasColumnType("decimal(18,4)");
            e.HasIndex(st => st.TicketNumber).IsUnique();

            e.HasOne(st => st.Customer).WithMany().HasForeignKey(st => st.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(st => st.Product).WithMany().HasForeignKey(st => st.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(st => st.Technician).WithMany().HasForeignKey(st => st.AssignedTo).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(st => st.Claim).WithOne(c => c.ServiceTicket).HasForeignKey<ServiceTicket>(st => st.ClaimId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(st => st.Parts).WithOne(sp => sp.ServiceTicket).HasForeignKey(sp => sp.ServiceTicketId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServicePart>(e =>
        {
            e.HasKey(sp => sp.Id);
            e.Property(sp => sp.Quantity).HasColumnType("decimal(18,4)");
            e.Property(sp => sp.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(sp => sp.LineTotal).HasColumnType("decimal(18,4)");
            e.HasOne(sp => sp.Product).WithMany().HasForeignKey(sp => sp.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ═══════════════════════════════════════════════════════════════════
        // CYCLE VENTE / ACHAT
        // ═══════════════════════════════════════════════════════════════════

        // ── DeliveryNote ──────────────────────────────────────────────────
        modelBuilder.Entity<DeliveryNote>(e =>
        {
            e.HasKey(dn => dn.Id);
            e.Property(dn => dn.DocNum).HasMaxLength(20).IsRequired();
            e.Property(dn => dn.Status).HasConversion<string>();
            e.HasIndex(dn => dn.DocNum).IsUnique();

            e.HasOne(dn => dn.Customer).WithMany().HasForeignKey(dn => dn.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(dn => dn.Order).WithMany().HasForeignKey(dn => dn.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(dn => dn.Lines).WithOne(l => l.DeliveryNote).HasForeignKey(l => l.DeliveryNoteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeliveryNoteLine>(e =>
        {
            e.HasKey(dnl => dnl.Id);
            e.Property(dnl => dnl.OrderedQty).HasColumnType("decimal(18,4)");
            e.Property(dnl => dnl.DeliveredQty).HasColumnType("decimal(18,4)");
            e.HasOne(dnl => dnl.Product).WithMany().HasForeignKey(dnl => dnl.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(dnl => dnl.OrderLine).WithMany().HasForeignKey(dnl => dnl.OrderLineId).OnDelete(DeleteBehavior.SetNull);
        });

        // ── Supplier ──────────────────────────────────────────────────────
        modelBuilder.Entity<Supplier>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.CardCode).HasMaxLength(15).IsRequired();
            e.Property(s => s.CardName).HasMaxLength(100).IsRequired();
            e.Property(s => s.Currency).HasMaxLength(5).HasDefaultValue("EUR");
            e.HasIndex(s => s.CardCode).IsUnique();
        });

        // ── PurchaseOrder ─────────────────────────────────────────────────
        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.HasKey(po => po.Id);
            e.Property(po => po.DocNum).HasMaxLength(20).IsRequired();
            e.Property(po => po.Status).HasConversion<string>();
            e.Property(po => po.DocTotal).HasColumnType("decimal(18,4)");
            e.Property(po => po.VatTotal).HasColumnType("decimal(18,4)");
            e.Property(po => po.Currency).HasMaxLength(5).HasDefaultValue("EUR");
            e.HasIndex(po => po.DocNum).IsUnique();

            e.HasOne(po => po.Supplier).WithMany(s => s.PurchaseOrders).HasForeignKey(po => po.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(po => po.Lines).WithOne(l => l.PurchaseOrder).HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrderLine>(e =>
        {
            e.HasKey(pol => pol.Id);
            e.Property(pol => pol.Quantity).HasColumnType("decimal(18,4)");
            e.Property(pol => pol.ReceivedQty).HasColumnType("decimal(18,4)");
            e.Property(pol => pol.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(pol => pol.VatPct).HasColumnType("decimal(5,2)");
            e.Property(pol => pol.LineTotal).HasColumnType("decimal(18,4)");
            e.HasOne(pol => pol.Product).WithMany().HasForeignKey(pol => pol.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── CreditNote ────────────────────────────────────────────────────
        modelBuilder.Entity<CreditNote>(e =>
        {
            e.HasKey(cn => cn.Id);
            e.Property(cn => cn.DocNum).HasMaxLength(20).IsRequired();
            e.Property(cn => cn.Status).HasConversion<string>();
            e.Property(cn => cn.Reason).HasConversion<string>();
            e.Property(cn => cn.DocTotal).HasColumnType("decimal(18,4)");
            e.Property(cn => cn.VatTotal).HasColumnType("decimal(18,4)");
            e.Property(cn => cn.Currency).HasMaxLength(5).HasDefaultValue("EUR");
            e.HasIndex(cn => cn.DocNum).IsUnique();

            e.HasOne(cn => cn.Customer).WithMany().HasForeignKey(cn => cn.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(cn => cn.Order).WithMany().HasForeignKey(cn => cn.OrderId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(cn => cn.Return).WithOne(r => r.CreditNote).HasForeignKey<CreditNote>(cn => cn.ReturnId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(cn => cn.Lines).WithOne(l => l.CreditNote).HasForeignKey(l => l.CreditNoteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CreditNoteLine>(e =>
        {
            e.HasKey(cnl => cnl.Id);
            e.Property(cnl => cnl.Quantity).HasColumnType("decimal(18,4)");
            e.Property(cnl => cnl.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(cnl => cnl.VatPct).HasColumnType("decimal(5,2)");
            e.Property(cnl => cnl.LineTotal).HasColumnType("decimal(18,4)");
            e.HasOne(cnl => cnl.Product).WithMany().HasForeignKey(cnl => cnl.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── GoodsReceipt ──────────────────────────────────────────────────
        modelBuilder.Entity<GoodsReceipt>(e =>
        {
            e.HasKey(gr => gr.Id);
            e.Property(gr => gr.DocNum).HasMaxLength(20).IsRequired();
            e.Property(gr => gr.Status).HasConversion<string>();
            e.HasIndex(gr => gr.DocNum).IsUnique();

            e.HasOne(gr => gr.Supplier).WithMany().HasForeignKey(gr => gr.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(gr => gr.PurchaseOrder).WithMany().HasForeignKey(gr => gr.PurchaseOrderId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(gr => gr.Lines).WithOne(l => l.GoodsReceipt).HasForeignKey(l => l.GoodsReceiptId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoodsReceiptLine>(e =>
        {
            e.HasKey(grl => grl.Id);
            e.Property(grl => grl.Quantity).HasColumnType("decimal(18,4)");
            e.Property(grl => grl.UnitPrice).HasColumnType("decimal(18,4)");
            e.Property(grl => grl.LineTotal).HasColumnType("decimal(18,4)");
            e.HasOne(grl => grl.Product).WithMany().HasForeignKey(grl => grl.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
