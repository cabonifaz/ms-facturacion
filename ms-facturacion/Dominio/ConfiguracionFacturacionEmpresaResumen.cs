namespace ms_facturacion.Dominio;

/// Proyección liviana para listados — SP_ConfiguracionFacturacionEmpresa_Listar solo devuelve estas columnas.
public sealed record ConfiguracionFacturacionEmpresaResumen(
    int IdConfiguracionFacturacionEmpresa, string AmbienteCodigo, string TipoProveedorCodigo, string? NombreProveedor, bool Activo);
