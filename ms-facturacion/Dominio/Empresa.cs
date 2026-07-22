namespace ms_facturacion.Dominio;

public sealed class Empresa
{
    public required int IdEmpresa { get; init; }
    public required int IdInquilino { get; init; }
    public required string Ruc { get; init; }
    public required string RazonSocial { get; init; }
    public string? NombreComercial { get; init; }
    public required string Direccion { get; init; }
    public required string Ubigeo { get; init; }
    public required string Departamento { get; init; }
    public required string Provincia { get; init; }
    public required string Distrito { get; init; }
    public required string PaisCodigo { get; init; }
    public required bool Activo { get; init; }
    public required DateTime FchCre { get; init; }
    public DateTime? FchMod { get; init; }
}
