namespace ms_facturacion.Dominio;

public sealed class ConfiguracionFacturacionEmpresa
{
    public required int IdConfiguracionFacturacionEmpresa { get; init; }
    public required int IdEmpresa { get; init; }
    public required string AmbienteCodigo { get; init; }
    public required string TipoProveedorCodigo { get; init; }
    public string? NombreProveedor { get; init; }
    public required int IdCertificado { get; init; }
    public string? UrlEnvioFacturaBoletaNota { get; init; }
    public string? UrlEnvioRetencionPercepcion { get; init; }
    public string? UrlEnvioGuiaRemision { get; init; }
    public string? UrlConsultaEstadoCdr { get; init; }
    public string? UrlConsultaValidez { get; init; }
    public required bool Activo { get; init; }
    public required DateTime FchCre { get; init; }
    public DateTime? FchMod { get; init; }
}
