namespace ms_facturacion.Dominio;

/// Datos para abrir un intento de transmisión (SP_TransmisionSunat_Insertar) — solo escritura en este pase.
/// Exactamente uno de IdDocumentoElectronico/IdLoteDocumento debe venir informado (sendBill vs sendSummary).
/// Ya no lleva referencias a archivos (IdArchivoSolicitud/IdArchivoXml) — esos vínculos ahora van al revés,
/// en ArchivoDocumento.IdTransmisionSunat, una vez que esta transmisión ya tiene su propio id.
public sealed record NuevaTransmisionSunat(
    int? IdDocumentoElectronico, int? IdLoteDocumento, string TipoProveedorCodigo, string Endpoint, string Metodo,
    int NumeroIntento);

/// Datos para cerrar un intento ya abierto (SP_TransmisionSunat_Actualizar) con el resultado real. Igual que
/// NuevaTransmisionSunat, ya no lleva referencias a archivos (IdArchivoRespuesta/IdArchivoPdf).
public sealed record ResultadoTransmisionSunat(
    EstadoMaestroCodigo EstadoCodigo, string? SunatCodigoEstado, string? SunatMensajeEstado,
    string? TipoError, string? MensajeError);
