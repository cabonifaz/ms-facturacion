namespace ms_facturacion.Dominio;

/// Datos para abrir un intento de transmisión (SP_TransmisionSunat_Insertar) — solo escritura en este pase.
public sealed record NuevaTransmisionSunat(
    int IdDocumentoElectronico, string TipoProveedorCodigo, string Endpoint, string Metodo,
    int? IdArchivoSolicitud, int NumeroIntento);

/// Datos para cerrar un intento ya abierto (SP_TransmisionSunat_Actualizar) con el resultado real.
public sealed record ResultadoTransmisionSunat(
    string EstadoCodigo, int? IdArchivoRespuesta, string? SunatCodigoEstado, string? SunatMensajeEstado,
    string? TipoError, string? MensajeError);
