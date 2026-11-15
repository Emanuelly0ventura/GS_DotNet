using AgroConnect.Application.DTOs;
using AgroConnect.Application.Pagination;
using AgroConnect.Application.Repositories;
using AgroConnect.Application.Services.Interfaces;
using AgroConnect.Domain.Entities;
using AgroConnect.Domain.Rules;

namespace AgroConnect.Application.Services.Implementations;

public sealed class PropertyService(IPropertyRepository repository) : IPropertyService
{
    public async Task<PageResponse<PropertyResponse>> ListAsync(Guid userId, int page, int size, CancellationToken ct)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);
        var (items, total) = await repository.ListAsync(userId, PageBuilder.Offset(page, size), size, ct);
        return PageBuilder.ToPage(items.Select(ToResponse).ToArray(), page, size, total);
    }

    public async Task<PropertyResponse?> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var property = await repository.FindAsync(userId, id, trackChanges: false, ct);
        return property is null ? null : ToResponse(property);
    }

    public PropertyWriteResult PrepareCreation(Guid userId, PropertyInput input)
    {
        var errors = Validate(input.Name, input.City, input.State, input.AreaHectares, input.Description);
        if (errors.Count != 0) return new(null, errors);

        var property = new Property
        {
            UserId = userId, Name = input.Name.Trim(), City = input.City.Trim(),
            State = input.State.Trim().ToUpperInvariant(), AreaHectares = input.AreaHectares, Description = input.Description
        };
        // The API's idempotency filter commits the property together with the replay record.
        repository.Add(property);
        return new(ToResponse(property), errors);
    }

    public async Task<PropertyWriteResult?> UpdateAsync(Guid userId, Guid id, PropertyPatch input, CancellationToken ct)
    {
        var property = await repository.FindAsync(userId, id, trackChanges: true, ct);
        if (property is null) return null;
        var description = input.HasDescription ? input.Description : property.Description;
        var name = input.Name ?? property.Name;
        var city = input.City ?? property.City;
        var state = input.State ?? property.State;
        var area = input.AreaHectares ?? property.AreaHectares;
        var errors = Validate(name, city, state, area, description);
        if (errors.Count != 0) return new(null, errors);

        var normalized = (name.Trim(), city.Trim(), state.Trim().ToUpperInvariant(), area, description);
        if ((property.Name, property.City, property.State, property.AreaHectares, property.Description) != normalized)
        {
            (property.Name, property.City, property.State, property.AreaHectares, property.Description) = normalized;
            property.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.SaveChangesAsync(ct);
        }
        return new(ToResponse(property), errors);
    }

    public Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct) => repository.DeleteAsync(userId, id, ct);

    private static PropertyResponse ToResponse(Property property) => new(property.Id, property.Name, property.City, property.State,
        property.AreaHectares, property.Description, property.CreatedAt, property.UpdatedAt);

    private static Dictionary<string, string> Validate(string? name, string? city, string? state, decimal area, string? description)
    {
        var fields = new Dictionary<string, string>();
        if (!DomainRules.IsValidName(name)) fields["name"] = "Informe um nome de 2 a 150 caracteres.";
        if (!DomainRules.IsValidCity(city)) fields["city"] = "Informe uma cidade de 2 a 120 caracteres.";
        if (!DomainRules.IsValidState(state?.Trim())) fields["state"] = "Informe a UF com duas letras.";
        if (!DomainRules.IsValidArea(area)) fields["areaHectares"] = "Informe uma área maior que zero.";
        if (description?.Length > 2000) fields["description"] = "Use até 2000 caracteres.";
        return fields;
    }
}
