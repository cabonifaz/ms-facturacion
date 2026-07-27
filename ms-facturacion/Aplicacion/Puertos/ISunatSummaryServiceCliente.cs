using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// sendSummary/getStatus — mismo billService/misma URL que sendBill, WS-Security UsernameToken igual.
public interface ISunatSummaryServiceCliente
{
    /// sendSummary: nunca devuelve el resultado final, solo un ticket para consultar después.
    Task<ResultadoOperacion<string>> EnviarAsync(
        string url, string usuarioSolCompleto, string claveSol, string nombreArchivoZip, byte[] zipBytes,
        CancellationToken cancellationToken);

    /// getStatus: 98=en proceso (CdrXmlBytes null), 0=procesado (CDR incluido), 99=error.
    Task<ResultadoOperacion<ResultadoConsultaTicket>> ConsultarAsync(
        string url, string usuarioSolCompleto, string claveSol, string ticket, CancellationToken cancellationToken);
}
