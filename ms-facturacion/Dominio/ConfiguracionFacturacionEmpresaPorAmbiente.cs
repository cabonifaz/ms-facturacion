namespace ms_facturacion.Dominio;

/// Proyección usada por el Worker (Módulo 4) para resolver la configuración vigente de una empresa
/// en un ambiente dado — matches SP_ConfiguracionFacturacionEmpresa_ObtenerPorEmpresaYAmbiente.
public sealed record ConfiguracionFacturacionEmpresaPorAmbiente(
    int IdConfiguracionFacturacionEmpresa, string TipoProveedorCodigo, string? NombreProveedor, int IdCertificado,
    string? UrlEnvioFacturaBoletaNota, string? UrlEnvioRetencionPercepcion, string? UrlEnvioGuiaRemision,
    string? UrlConsultaEstadoCdr, string? UrlConsultaValidez);
