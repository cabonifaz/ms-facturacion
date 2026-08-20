namespace ms_facturacion.Dominio;

/// Estados de TABLA_MAESTRA IdMaestro=1 — los valores numéricos coinciden con Num1 en
/// 03_LlenarTablaMaestra_MsFacturacion.sql. Las 6 SP que resuelven un nuevo estado (SP_DocumentoElectronico_
/// Insertar/ActualizarEstadoSunat, SP_LoteDocumento_Insertar/ActualizarEstadoSunat, SP_TransmisionSunat_
/// Insertar/Actualizar) matchean por Num1, no por el string — este enum es el valor que via por el wire.
public enum EstadoMaestroCodigo
{
    PendienteEnvio = 1,
    Enviando = 2,
    Aceptado = 3,
    AceptadoConObservaciones = 4,
    Rechazado = 5,
    ComunicacionBajaEnviada = 6,
    ComunicacionBajaAceptada = 7,
    ErrorSunat = 8,
    TicketRecibido = 9,
    TicketPendiente = 10,
    ConsultandoTicket = 11,
    TicketConError = 12,
    ComunicacionBajaRechazada = 13,
    ComunicacionBajaConError = 14,
    AnuladoManualmente = 15,
    ResumenBajaEnviado = 16,
    ResumenBajaAceptado = 17,
    ResumenBajaRechazado = 18,
    ResumenBajaConError = 19
}
