namespace ms_facturacion.Dominio;

public sealed class DocumentoElectronico
{
    public required int IdDocumentoElectronico { get; init; }
    public required int IdEmpresa { get; init; }
    public required string IdExterno { get; init; }
    public required string SistemaOrigen { get; init; }
    public required string TipoDocumentoCodigo { get; init; }
    public required string Serie { get; init; }
    public required int Correlativo { get; init; }
    public required string EstadoCodigo { get; init; }
    public required DateOnly FechaEmision { get; init; }
    public required TimeOnly HoraEmision { get; init; }
    public required string MonedaCodigo { get; init; }
    public required string TipoOperacionCodigo { get; init; }

    /// No es una columna persistida — SP_DocumentoElectronico_Obtener la resuelve contra
    /// TABLA_MAESTRA IdMaestro=9 según haya o no cuotas activas ("Contado"/"Credito").
    public required string FormaPagoCodigo { get; init; }

    public required string EmpresaRuc { get; init; }
    public required string EmpresaRazonSocial { get; init; }
    public string? EmpresaNombreComercial { get; init; }
    public required string EmpresaDireccion { get; init; }
    public required string EmpresaUbigeo { get; init; }

    public required string ClienteTipoDocumentoCodigo { get; init; }
    public required string ClienteNumeroDocumento { get; init; }
    public required string ClienteNombre { get; init; }
    public string? ClienteDireccion { get; init; }
    public string? ClienteCorreo { get; init; }
    public required string ClientePaisCodigo { get; init; }

    public required decimal TotalGravado { get; init; }
    public required decimal TotalInafecto { get; init; }
    public required decimal TotalExonerado { get; init; }
    public required decimal TotalGratuito { get; init; }
    public required decimal TotalIgv { get; init; }
    public required decimal TotalIsc { get; init; }
    public required decimal TotalOtrosTributos { get; init; }
    public required decimal TotalDescuento { get; init; }
    public required decimal TotalCargo { get; init; }
    public required decimal TotalImporte { get; init; }

    public string? SunatHash { get; init; }
    public string? SunatCodigoRespuesta { get; init; }
    public string? SunatDescripcionRespuesta { get; init; }
    public string? SunatTicket { get; init; }
    public DateTime? FechaAceptacion { get; init; }
    public DateTime? FechaRechazo { get; init; }
    public DateTime? FechaAnulacion { get; init; }

    public required DateTime FchCre { get; init; }
}
