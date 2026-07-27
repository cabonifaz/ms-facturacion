namespace ms_facturacion.Dominio;

public sealed class SerieDocumento
{
    public required int IdSerieDocumento { get; init; }
    public required int IdInquilino { get; init; }
    public required int IdEmpresa { get; init; }
    public required string TipoDocumentoCodigo { get; init; }
    public required string Serie { get; init; }
    public required int NumeroActual { get; init; }
    public required bool Activo { get; init; }
    public required DateTime FchCre { get; init; }
    public DateTime? FchMod { get; init; }
}
