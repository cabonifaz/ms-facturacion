namespace ms_facturacion.Dominio;

public sealed class Inquilino
{
    public required int IdInquilino { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public required bool Activo { get; init; }
    public required DateTime FchCre { get; init; }
    public DateTime? FchMod { get; init; }
}
