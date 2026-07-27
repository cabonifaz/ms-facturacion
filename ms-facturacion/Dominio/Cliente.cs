namespace ms_facturacion.Dominio;

public sealed class Cliente
{
    public required int IdCliente { get; init; }
    public required int IdInquilino { get; init; }
    public required string TipoDocumentoCodigo { get; init; }
    public required string NumeroDocumento { get; init; }
    public required string Nombre { get; init; }
    public string? Correo { get; init; }
    public string? Direccion { get; init; }
    public required string PaisCodigo { get; init; }
    public required DateTime FchCre { get; init; }
    public DateTime? FchMod { get; init; }
}
