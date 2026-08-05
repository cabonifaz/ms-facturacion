using System.Security.Cryptography;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;

/// Consulta getStatus una vez para un lote con ticket pendiente. 98 → TicketPendiente (éxito, "seguir
/// esperando"); 0 → interpreta el CDR y cierra el lote + todos sus items + cada documento afectado
/// (reutilizando IDocumentoElectronicoRepositorio.ActualizarEstadoSunatAsync ya existente, para que
/// dispare FechaAnulacion); 99 → TicketConError. Depende solo de Puertos.
public sealed class ConsultarTicketComunicacionBajaCasoDeUso(
    ILoteDocumentoRepositorio loteRepositorio,
    IItemLoteDocumentoRepositorio itemRepositorio,
    IDocumentoElectronicoRepositorio documentoRepositorio,
    IEmpresaRepositorio empresaRepositorio,
    IConfiguracionFacturacionEmpresaRepositorio configuracionRepositorio,
    ICredencialInquilinoRepositorio credencialRepositorio,
    ICifradoInquilinoServicio cifradoServicio,
    IAlmacenamientoArchivosServicio almacenamiento,
    IArchivoDocumentoRepositorio archivoRepositorio,
    ISunatSummaryServiceCliente sunatCliente,
    IErrorDocumentoRepositorio errorRepositorio)
{
    private const string UsuarioWorker = "ms-facturacion-worker";

    public async Task<ResultadoOperacion<ResultadoConsultaTicket>> EjecutarAsync(
        int idInquilino, int idLoteDocumento, string ambienteCodigo, CancellationToken cancellationToken)
    {
        var lote = await loteRepositorio.ObtenerAsync(idInquilino, idLoteDocumento, cancellationToken);
        if (lote.IdTipoMensaje != TipoMensaje.Exito || lote.Datos is null)
        {
            return new ResultadoOperacion<ResultadoConsultaTicket>(lote.IdTipoMensaje, lote.Mensaje, default);
        }

        var cabecera = lote.Datos.Cabecera;

        if (cabecera.EstadoCodigo is not ("TicketRecibido" or "TicketPendiente" or "TicketConError"))
        {
            return ResultadoOperacion<ResultadoConsultaTicket>.DeReglaDeNegocio(
                $"El lote no tiene un ticket pendiente de consulta (estado actual: {cabecera.EstadoCodigo}).");
        }

        if (string.IsNullOrWhiteSpace(cabecera.Ticket))
        {
            return ResultadoOperacion<ResultadoConsultaTicket>.DeReglaDeNegocio("El lote no tiene un ticket registrado.");
        }

        var empresa = await empresaRepositorio.ObtenerAsync(idInquilino, cabecera.IdEmpresa, cancellationToken);
        if (empresa.IdTipoMensaje != TipoMensaje.Exito || empresa.Datos is null)
        {
            return new ResultadoOperacion<ResultadoConsultaTicket>(empresa.IdTipoMensaje, empresa.Mensaje, default);
        }

        var configuracion = await configuracionRepositorio.ObtenerPorEmpresaYAmbienteAsync(idInquilino, cabecera.IdEmpresa, ambienteCodigo, cancellationToken);
        if (configuracion.IdTipoMensaje != TipoMensaje.Exito || configuracion.Datos is null)
        {
            return new ResultadoOperacion<ResultadoConsultaTicket>(configuracion.IdTipoMensaje, configuracion.Mensaje, default);
        }

        if (string.IsNullOrWhiteSpace(configuracion.Datos.UrlEnvioFacturaBoletaNota))
        {
            return ResultadoOperacion<ResultadoConsultaTicket>.DeReglaDeNegocio(
                "La configuración de facturación de la empresa no tiene URL de envío (billService).");
        }

        var claveSol = await credencialRepositorio.ObtenerPorTipoAsync(idInquilino, cabecera.IdEmpresa, "ClaveSol", cancellationToken);
        if (claveSol.IdTipoMensaje != TipoMensaje.Exito || claveSol.Datos is null)
        {
            return new ResultadoOperacion<ResultadoConsultaTicket>(claveSol.IdTipoMensaje, claveSol.Mensaje, default);
        }

        var claveSolDescifrada = await cifradoServicio.DescifrarAsync(
            idInquilino, claveSol.Datos.ValorCifrado, claveSol.Datos.Nonce, claveSol.Datos.Tag, cancellationToken);
        if (claveSolDescifrada.IdTipoMensaje != TipoMensaje.Exito || claveSolDescifrada.Datos is null)
        {
            return new ResultadoOperacion<ResultadoConsultaTicket>(claveSolDescifrada.IdTipoMensaje, claveSolDescifrada.Mensaje, default);
        }

        var usuarioSolCompleto = empresa.Datos.Ruc + claveSol.Datos.Usuario;

        var consulta = await sunatCliente.ConsultarAsync(
            configuracion.Datos.UrlEnvioFacturaBoletaNota, usuarioSolCompleto, claveSolDescifrada.Datos, cabecera.Ticket, cancellationToken);

        if (consulta.IdTipoMensaje != TipoMensaje.Exito || consulta.Datos is null)
        {
            return new ResultadoOperacion<ResultadoConsultaTicket>(consulta.IdTipoMensaje, consulta.Mensaje, default);
        }

        // 98: todavía en proceso — solo se refleja el estado, no hay CDR que interpretar aún.
        if (consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.TicketPendiente)
        {
            await loteRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketPendiente, cabecera.Ticket, null, null, cancellationToken);

            return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(consulta.Mensaje, consulta.Datos);
        }

        // 99: error técnico de SUNAT al procesar el ticket, sin CDR utilizable.
        if (consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.TicketConError)
        {
            await loteRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, cabecera.Ticket, null, null, cancellationToken);
            await itemRepositorio.ActualizarEstadoSunatTodosAsync(
                UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, null, null, cancellationToken);

            return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(consulta.Mensaje, consulta.Datos);
        }

        // 0: procesado — guardar CDR, cerrar lote/items/documentos con el resultado real.
        var carpeta = $"{idInquilino}/{cabecera.IdEmpresa}/{cabecera.FechaReferencia:yyyy}/{cabecera.FechaReferencia:MM}/baja-{cabecera.Nombre}";
        await GuardarCdrAsync(idInquilino, idLoteDocumento, carpeta, cabecera.Nombre, consulta.Datos.CdrXmlBytes!, cancellationToken);

        await loteRepositorio.ActualizarEstadoSunatAsync(
            UsuarioWorker, idInquilino, idLoteDocumento, consulta.Datos.EstadoCodigo, cabecera.Ticket,
            consulta.Datos.SunatCodigoRespuesta, consulta.Datos.SunatDescripcionRespuesta, cancellationToken);

        await itemRepositorio.ActualizarEstadoSunatTodosAsync(
            UsuarioWorker, idInquilino, idLoteDocumento, consulta.Datos.EstadoCodigo,
            consulta.Datos.SunatCodigoRespuesta, consulta.Datos.SunatDescripcionRespuesta, cancellationToken);

        var esAceptado = consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.ComunicacionBajaAceptada;
        var severidad = consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.Rechazado ? "Error" : "Advertencia";

        foreach (var item in lote.Datos.Items)
        {
            await documentoRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, item.IdDocumentoElectronico, consulta.Datos.EstadoCodigo,
                null, consulta.Datos.SunatCodigoRespuesta, consulta.Datos.SunatDescripcionRespuesta, null, cancellationToken);

            if (!esAceptado)
            {
                await errorRepositorio.InsertarAsync(
                    UsuarioWorker, idInquilino,
                    new ErrorDocumento(
                        item.IdDocumentoElectronico, null, "Sunat",
                        consulta.Datos.SunatCodigoRespuesta ?? string.Empty, consulta.Datos.SunatDescripcionRespuesta ?? string.Empty,
                        null, severidad),
                    cancellationToken);
            }
        }

        return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(consulta.Mensaje, consulta.Datos);
    }

    private async Task GuardarCdrAsync(
        int idInquilino, int idLoteDocumento, string carpeta, string nombreLote, byte[] cdrXmlBytes, CancellationToken cancellationToken)
    {
        var nombreArchivo = $"{nombreLote}-{DateTime.UtcNow:yyyyMMddHHmmss}.cdr";
        var ruta = await almacenamiento.GuardarAsync(carpeta, nombreArchivo, cdrXmlBytes, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(cdrXmlBytes)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(null, idLoteDocumento, "Cdr", nombreArchivo, ruta, "application/xml", hash, cdrXmlBytes.LongLength);
        await archivoRepositorio.InsertarAsync(UsuarioWorker, idInquilino, archivo, cancellationToken);
    }
}
