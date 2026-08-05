namespace ms_facturacion.Dominio;

/// Datos para abrir un intento de transmisión (SP_TransmisionSunat_Insertar) — solo escritura en este pase.
/// Exactamente uno de IdDocumentoElectronico/IdLoteDocumento debe venir informado (sendBill vs sendSummary).
public sealed record NuevaTransmisionSunat(
    int? IdDocumentoElectronico, int? IdLoteDocumento, string TipoProveedorCodigo, string Endpoint, string Metodo,
    int? IdArchivoSolicitud, int NumeroIntento, int? IdArchivoXml = null);

/// Datos para cerrar un intento ya abierto (SP_TransmisionSunat_Actualizar) con el resultado real.
/// IdArchivoPdf llega después que el resto (la representación impresa se genera recién tras la aceptación).
public sealed record ResultadoTransmisionSunat(
    EstadoMaestroCodigo EstadoCodigo, int? IdArchivoRespuesta, string? SunatCodigoEstado, string? SunatMensajeEstado,
    string? TipoError, string? MensajeError, int? IdArchivoPdf = null);
