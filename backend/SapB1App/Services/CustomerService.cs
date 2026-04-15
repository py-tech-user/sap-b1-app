using Microsoft.EntityFrameworkCore;
using SapB1App.Data;
using SapB1App.DTOs;
using SapB1App.Interfaces;
using SapB1App.Models;

namespace SapB1App.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext  _db;
    private readonly ISapB1Service _sap;

    // Mappings pour les labels des groupes
    private static readonly Dictionary<CustomerGroup, string> GroupLabels = new()
    {
        { CustomerGroup.Etranger, "Etranger" },
        { CustomerGroup.GroupeScolaire, "Groupe scolaire" },
        { CustomerGroup.LesParticuliersGP, "Les particuliers GP" },
        { CustomerGroup.LesRevendeurs, "Les revendeurs" },
        { CustomerGroup.LesSallesDeSports, "Les salles de sports" },
        { CustomerGroup.Locaux, "Locaux" },
        { CustomerGroup.OrganismePublic, "Organisme public" }
    };

    // Mappings pour les labels des devises
    private static readonly Dictionary<CurrencyType, string> CurrencyLabels = new()
    {
        { CurrencyType.CHF, "CHF" },
        { CurrencyType.DKK, "DKK" },
        { CurrencyType.EUR, "Euro" },
        { CurrencyType.GBP, "GBP" },
        { CurrencyType.JPY, "JPY" },
        { CurrencyType.MAD, "MAD" },
        { CurrencyType.NOK, "NOK" },
        { CurrencyType.SEK, "SEK" },
        { CurrencyType.USD, "USD" },
        { CurrencyType.ToutesDevises, "Toutes devises" }
    };

    public CustomerService(AppDbContext db, ISapB1Service sap)
    {
        _db  = db;
        _sap = sap;
    }

    // ── GET OPTIONS (pour les listes déroulantes) ─────────────────────────────
    public CustomerOptionsDto GetOptions()
    {
        return new CustomerOptionsDto
        {
            PartnerTypes = new List<OptionDto>
            {
                new() { Value = "Client", Label = "Client" },
                new() { Value = "Prospect", Label = "Prospect" }
            },
            Groups = GroupLabels.Select(g => new OptionDto 
            { 
                Value = g.Key.ToString(), 
                Label = g.Value 
            }).ToList(),
            Currencies = CurrencyLabels.Select(c => new OptionDto 
            { 
                Value = c.Key.ToString(), 
                Label = c.Value 
            }).ToList()
        };
    }

    // ── GET ALL (paged + search + partnerType filter) ─────────────────────────
    public async Task<PagedResult<CustomerDto>> GetAllAsync(
        int page, int pageSize, string? search, string? partnerType)
    {
        var query = _db.Customers
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c =>
                c.CardCode.Contains(search) ||
                c.CardName.Contains(search) ||
                (c.ForeignName != null && c.ForeignName.Contains(search)) ||
                (c.FederalTaxId != null && c.FederalTaxId.Contains(search)));

        // Filtre par type de partenaire
        if (!string.IsNullOrWhiteSpace(partnerType) && 
            Enum.TryParse<PartnerType>(partnerType, true, out var type))
        {
            query = query.Where(c => c.PartnerType == type);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(c => c.CardCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Charger les counts des commandes séparément (optimisation)
        var customerIds = items.Select(c => c.Id).ToList();
        var orderCounts = await _db.Orders
            .AsNoTracking()
            .Where(o => customerIds.Contains(o.CustomerId))
            .GroupBy(o => o.CustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count() })
            .ToListAsync();

        var orderCountDict = orderCounts.ToDictionary(x => x.CustomerId, x => x.Count);

        return new PagedResult<CustomerDto>
        {
            Items      = items.Select(c => MapToDto(c, orderCountDict.GetValueOrDefault(c.Id, 0))),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        };
    }

    // ── GET BY ID ────────────────────────────────────────────────────────────
    public async Task<CustomerDto?> GetByIdAsync(int id)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer is null) return null;

        var orderCount = await _db.Orders
            .AsNoTracking()
            .CountAsync(o => o.CustomerId == id);

        return MapToDto(customer, orderCount);
    }

    // ── CREATE ───────────────────────────────────────────────────────────────
    public async Task<CustomerDto> CreateAsync(CreateCustomerDto dto)
    {
        var code = dto.CardCode.Trim().ToUpperInvariant();

        if (await _db.Customers.AsNoTracking().AnyAsync(c => c.CardCode == code))
            throw new InvalidOperationException(
                $"Le code partenaire '{code}' existe déjà.");

        // Parse le type de partenaire
        var partnerType = PartnerType.Client;
        if (!string.IsNullOrWhiteSpace(dto.PartnerType) &&
            Enum.TryParse<PartnerType>(dto.PartnerType, true, out var parsedType))
        {
            partnerType = parsedType;
        }

        // Parse le groupe
        var groupCode = CustomerGroup.Locaux;
        if (!string.IsNullOrWhiteSpace(dto.GroupCode) &&
            Enum.TryParse<CustomerGroup>(dto.GroupCode, true, out var parsedGroup))
        {
            groupCode = parsedGroup;
        }

        // Parse la devise
        var currency = CurrencyType.EUR;
        if (!string.IsNullOrWhiteSpace(dto.Currency) &&
            Enum.TryParse<CurrencyType>(dto.Currency, true, out var parsedCurrency))
        {
            currency = parsedCurrency;
        }

        var phone1 = Normalize(dto.Phone1);
        var phone  = Normalize(dto.Phone);
        if (phone1 is null) phone1 = phone;
        if (phone is null) phone = phone1;

        var customer = new Customer
        {
            CardCode     = code,
            PartnerType  = partnerType,
            CardName     = dto.CardName.Trim(),
            ForeignName  = dto.ForeignName?.Trim(),
            GroupCode    = groupCode,
            Currency     = currency,
            FederalTaxId = dto.FederalTaxId?.Trim(),
            Phone        = phone,
            Phone1       = phone1,
            Phone2       = Normalize(dto.Phone2),
            MobilePhone  = Normalize(dto.MobilePhone),
            Email        = Normalize(dto.Email),
            Contact      = Normalize(dto.Contact),
            AdditionalIdentificationNumber = Normalize(dto.AdditionalIdentificationNumber),
            UnifiedTaxIdentificationNumber = Normalize(dto.UnifiedTaxIdentificationNumber),
            CreatedAt    = DateTime.UtcNow
        };

        var sapResult = await _sap.CreateBusinessPartnerAsync(customer);
        if (!sapResult.Success)
        {
            throw new InvalidOperationException(sapResult.ErrorMessage ?? "Erreur Service Layer lors de la création du Business Partner.");
        }

        if (sapResult.Response is { } response &&
            response.TryGetProperty("DocEntry", out var docEntry) &&
            docEntry.TryGetInt32(out var sapDocNum))
        {
            customer.SapDocNum = sapDocNum;
        }

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        return MapToDto(customer, 0);
    }

    // ── UPDATE ───────────────────────────────────────────────────────────────
    public async Task<CustomerDto?> UpdateAsync(int id, UpdateCustomerDto dto)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return null;

        // Attacher l'entité pour le tracking (nécessaire car NoTracking est activé globalement)
        _db.Customers.Attach(customer);

        // Parse le type de partenaire
        if (!string.IsNullOrWhiteSpace(dto.PartnerType) &&
            Enum.TryParse<PartnerType>(dto.PartnerType, true, out var parsedType))
        {
            customer.PartnerType = parsedType;
        }

        // Parse le groupe
        if (!string.IsNullOrWhiteSpace(dto.GroupCode) &&
            Enum.TryParse<CustomerGroup>(dto.GroupCode, true, out var parsedGroup))
        {
            customer.GroupCode = parsedGroup;
        }

        // Parse la devise
        if (!string.IsNullOrWhiteSpace(dto.Currency) &&
            Enum.TryParse<CurrencyType>(dto.Currency, true, out var parsedCurrency))
        {
            customer.Currency = parsedCurrency;
        }

        var phone1 = Normalize(dto.Phone1);
        var phone  = Normalize(dto.Phone);
        if (phone1 is null) phone1 = phone;
        if (phone is null) phone = phone1;

        customer.CardName     = dto.CardName.Trim();
        customer.ForeignName  = dto.ForeignName?.Trim();
        customer.FederalTaxId = dto.FederalTaxId?.Trim();
        customer.Phone        = phone;
        customer.Phone1       = phone1;
        customer.Phone2       = Normalize(dto.Phone2);
        customer.MobilePhone  = Normalize(dto.MobilePhone);
        customer.Email        = Normalize(dto.Email);
        customer.Contact      = Normalize(dto.Contact);
        customer.AdditionalIdentificationNumber = Normalize(dto.AdditionalIdentificationNumber);
        customer.UnifiedTaxIdentificationNumber = Normalize(dto.UnifiedTaxIdentificationNumber);
        customer.UpdatedAt    = DateTime.UtcNow;

        // Marquer l'entité comme modifiée pour forcer la mise à jour
        _db.Entry(customer).State = EntityState.Modified;

        await _db.SaveChangesAsync();

        var orderCount = await _db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == id);
        return MapToDto(customer, orderCount);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return false;

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();
        return true;
    }

    // ── SYNC TO SAP B1 ───────────────────────────────────────────────────────
    public async Task<CustomerDto?> SyncToSapAsync(int id)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer is null) return null;

        await _sap.SyncCustomerAsync(id);

        // Simule un DocEntry retourné par SAP (remplacer par la vraie valeur)
        customer.SapDocNum = new Random().Next(1000, 9999);
        customer.UpdatedAt = DateTime.UtcNow;

        _db.Entry(customer).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        await _db.SaveChangesAsync();

        var orderCount = await _db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == id);
        return MapToDto(customer, orderCount);
    }

    // ── MAPPER ───────────────────────────────────────────────────────────────
    private static CustomerDto MapToDto(Customer c, int orderCount = 0) => new()
    {
        Id           = c.Id,
        CardCode     = c.CardCode,
        PartnerType  = c.PartnerType.ToString(),
        CardName     = c.CardName,
        ForeignName  = c.ForeignName,
        GroupCode    = c.GroupCode.ToString(),
        Currency     = c.Currency.ToString(),
        FederalTaxId = c.FederalTaxId,
        Phone        = c.Phone ?? c.Phone1,
        Phone1       = c.Phone1 ?? c.Phone,
        Phone2       = c.Phone2,
        MobilePhone  = c.MobilePhone,
        Email        = c.Email,
        Contact      = c.Contact,
        AdditionalIdentificationNumber = c.AdditionalIdentificationNumber,
        UnifiedTaxIdentificationNumber = c.UnifiedTaxIdentificationNumber,
        SyncedToSap  = c.SapDocNum.HasValue,
        CreatedAt    = c.CreatedAt,
        OrderCount   = orderCount
    };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
