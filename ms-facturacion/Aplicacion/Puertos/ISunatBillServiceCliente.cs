using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Envía un comprobante a billService (sendBill) con WS-Security UsernameToken y decodifica el CDR —
/// ver facturacion/payload_input_output_sunat.md §2.2/§2.3 para el envelope exacto.
public interface ISunatBillServiceCliente
{
    /// usuarioSolCompleto ya debe venir concatenado (EMPRESAS.Ruc + CREDENCIALES_INQUILINO.Usuario) —
    /// ese armado es responsabilidad del caso de uso llamador, no de este cliente.
    Task<ResultadoOperacion<ResultadoEnvioSunat>> EnviarAsync(
        string url, string usuarioSolCompleto, string claveSol, string nombreArchivoZip, byte[] zipBytes,
        CancellationToken cancellationToken);
}
