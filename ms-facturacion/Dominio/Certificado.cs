namespace ms_facturacion.Dominio;

public sealed class Certificado
{
    public required int IdCertificado { get; init; }
    public required int IdEmpresa { get; init; }
    public required string RutaAlmacenamiento { get; init; }
    public required string Sujeto { get; init; }
    public required string Emisor { get; init; }
    public required string NumeroSerie { get; init; }
    public required string HuellaDigital { get; init; }
    public required DateOnly ValidoDesde { get; init; }
    public required DateOnly ValidoHasta { get; init; }
    public required bool Activo { get; init; }
    public required DateTime FchCre { get; init; }
    public DateTime? FchMod { get; init; }
}
