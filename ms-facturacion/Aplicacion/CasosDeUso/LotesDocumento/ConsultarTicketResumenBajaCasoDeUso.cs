using System.Security.Cryptography;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;

/// Mismo caso de uso que ConsultarTicketComunicacionBajaCasoDeUso, para lotes TipoLoteCodigo=
/// 'ResumenBajaBoleta' — necesario porque los estados de destino (ResumenBaja* en vez de ComunicacionBaja*)
/// están hardcodeados en la interpretación del CDR, no se puede parametrizar sin duplicar el flujo.
public sealed class ConsultarTicketResumenBajaCasoDeUso(
    ILoteDocumentoRepositorio loteRepositorio,
    IItemLoteDocumentoRepositorio itemRepositorio,
    IDocumentoElectronicoRepositorio documentoRepositorio,
    IEmpresaRepositorio empresaRepositorio,
    IConfiguracionFacturacionEmpresaRepositorio configuracionRepositorio,
    ICredencialInquilinoRepositorio credencialRepositorio,
    ICifradoInquilinoServicio cifradoServicio,
    IAlmacenamientoArchivosServicio almacenamiento,
    IArchivoDocumentoRepositorio archivoRepositorio,
    ITransmisionSunatRepositorio transmisionRepositorio,
    ISunatSummaryServiceCliente sunatCliente,
    IErrorDocumentoRepositorio errorRepositorio,
    IGeneradorPdfComprobanteServicio generadorPdf,
    ILogger<ConsultarTicketResumenBajaCasoDeUso> logger)
{
    private const string UsuarioWorker = "ms-facturacion-worker";

    public async Task<ResultadoOperacion<ResultadoConsultaTicket>> EjecutarAsync(
        int idInquilino, int idLoteDocumento, string ambienteCodigo, CancellationToken cancellationToken)
    {
        try
        {
            return await EjecutarInternoAsync(idInquilino, idLoteDocumento, ambienteCodigo, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "ConsultarTicketResumenBaja — excepción no controlada. idInquilino={IdInquilino}, idLoteDocumento={IdLoteDocumento}, ambienteCodigo={AmbienteCodigo}.",
                idInquilino, idLoteDocumento, ambienteCodigo);

            return ResultadoOperacion<ResultadoConsultaTicket>.DeErrorSistema(ex.Message);
        }
    }

    private void LogSiErrorSistema(TipoMensaje idTipoMensaje, string mensaje, int idLoteDocumento, string contexto)
    {
        if (idTipoMensaje == TipoMensaje.ErrorSistema)
        {
            logger.LogError(
                "ConsultarTicketResumenBaja — {Contexto}. idLoteDocumento={IdLoteDocumento}: {Mensaje}",
                contexto, idLoteDocumento, mensaje);
        }
    }

    private async Task<ResultadoOperacion<ResultadoConsultaTicket>> EjecutarInternoAsync(
        int idInquilino, int idLoteDocumento, string ambienteCodigo, CancellationToken cancellationToken)
    {
        var lote = await loteRepositorio.ObtenerAsync(idInquilino, idLoteDocumento, cancellationToken);
        if (lote.IdTipoMensaje != TipoMensaje.Exito || lote.Datos is null)
        {
            LogSiErrorSistema(lote.IdTipoMensaje, lote.Mensaje, idLoteDocumento, "falló al obtener el lote");
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

        var empresaTask = empresaRepositorio.ObtenerAsync(idInquilino, cabecera.IdEmpresa, cancellationToken);
        var configuracionTask = configuracionRepositorio.ObtenerPorEmpresaYAmbienteAsync(idInquilino, cabecera.IdEmpresa, ambienteCodigo, cancellationToken);
        var claveSolTask = credencialRepositorio.ObtenerPorTipoAsync(idInquilino, cabecera.IdEmpresa, "ClaveSol", cancellationToken);

        await Task.WhenAll(empresaTask, configuracionTask, claveSolTask);

        var empresa = await empresaTask;
        if (empresa.IdTipoMensaje != TipoMensaje.Exito || empresa.Datos is null)
        {
            LogSiErrorSistema(empresa.IdTipoMensaje, empresa.Mensaje, idLoteDocumento, "falló al obtener la empresa");
            return new ResultadoOperacion<ResultadoConsultaTicket>(empresa.IdTipoMensaje, empresa.Mensaje, default);
        }

        var configuracion = await configuracionTask;
        if (configuracion.IdTipoMensaje != TipoMensaje.Exito || configuracion.Datos is null)
        {
            LogSiErrorSistema(configuracion.IdTipoMensaje, configuracion.Mensaje, idLoteDocumento, "falló al obtener la configuración de facturación");
            return new ResultadoOperacion<ResultadoConsultaTicket>(configuracion.IdTipoMensaje, configuracion.Mensaje, default);
        }

        if (string.IsNullOrWhiteSpace(configuracion.Datos.UrlEnvioFacturaBoletaNota))
        {
            return ResultadoOperacion<ResultadoConsultaTicket>.DeReglaDeNegocio(
                "La configuración de facturación de la empresa no tiene URL de envío (billService).");
        }

        var claveSol = await claveSolTask;
        if (claveSol.IdTipoMensaje != TipoMensaje.Exito || claveSol.Datos is null)
        {
            LogSiErrorSistema(claveSol.IdTipoMensaje, claveSol.Mensaje, idLoteDocumento, "falló al obtener la credencial ClaveSol");
            return new ResultadoOperacion<ResultadoConsultaTicket>(claveSol.IdTipoMensaje, claveSol.Mensaje, default);
        }

        var claveSolDescifrada = await cifradoServicio.DescifrarAsync(
            idInquilino, claveSol.Datos.ValorCifrado, claveSol.Datos.Nonce, claveSol.Datos.Tag, cancellationToken);
        if (claveSolDescifrada.IdTipoMensaje != TipoMensaje.Exito || claveSolDescifrada.Datos is null)
        {
            LogSiErrorSistema(claveSolDescifrada.IdTipoMensaje, claveSolDescifrada.Mensaje, idLoteDocumento, "falló al descifrar la ClaveSol");
            return new ResultadoOperacion<ResultadoConsultaTicket>(claveSolDescifrada.IdTipoMensaje, claveSolDescifrada.Mensaje, default);
        }

        var usuarioSolCompleto = empresa.Datos.Ruc + claveSol.Datos.Usuario;

        var consulta = await sunatCliente.ConsultarAsync(
            configuracion.Datos.UrlEnvioFacturaBoletaNota, usuarioSolCompleto, claveSolDescifrada.Datos, cabecera.Ticket, cancellationToken);

        if (consulta.IdTipoMensaje != TipoMensaje.Exito || consulta.Datos is null)
        {
            LogSiErrorSistema(consulta.IdTipoMensaje, consulta.Mensaje, idLoteDocumento, "getStatus falló");
            return new ResultadoOperacion<ResultadoConsultaTicket>(consulta.IdTipoMensaje, consulta.Mensaje, default);
        }

        // 98: todavía en proceso.
        if (consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.TicketPendiente)
        {
            await loteRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketPendiente, cabecera.Ticket, null, null, cancellationToken);

            return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(consulta.Mensaje, consulta.Datos);
        }

        // 99: error técnico de SUNAT, sin CDR utilizable — ResumenBajaConError (no el genérico Error), mismo
        // criterio que ConsultarTicketComunicacionBajaCasoDeUso.
        if (consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.TicketConError)
        {
            await loteRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, cabecera.Ticket, null, null, cancellationToken);
            await itemRepositorio.ActualizarEstadoSunatTodosAsync(
                UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, null, null, cancellationToken);

            var ahoraError = RelojPeru.Ahora();
            await Task.WhenAll(lote.Datos.Items.Select(item => Task.WhenAll(
                documentoRepositorio.ActualizarEstadoSunatAsync(
                    UsuarioWorker, idInquilino, item.IdDocumentoElectronico, EstadoMaestroCodigo.ResumenBajaConError,
                    null, null, null, null, ahoraError, cancellationToken),
                errorRepositorio.InsertarAsync(
                    UsuarioWorker, idInquilino,
                    new ErrorDocumento(
                        item.IdDocumentoElectronico, null, "Sunat",
                        consulta.Datos.SunatCodigoRespuesta ?? string.Empty, consulta.Mensaje,
                        null, "Error"),
                    cancellationToken))));

            return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(consulta.Mensaje, consulta.Datos);
        }

        // 0: procesado — guardar CDR, cerrar lote/items/documentos con el resultado real.
        var carpeta = $"{idInquilino}/{cabecera.IdEmpresa}/{cabecera.FechaReferencia:yyyy}/{cabecera.FechaReferencia:MM}/resumen-baja-{cabecera.Nombre}";

        int? idTransmisionSunat = null;
        var nuevaTransmisionTicket = new NuevaTransmisionSunat(
            null, idLoteDocumento, configuracion.Datos.TipoProveedorCodigo, configuracion.Datos.UrlEnvioFacturaBoletaNota, "getStatus", 1);
        var transmisionTicket = await transmisionRepositorio.InsertarAsync(UsuarioWorker, idInquilino, nuevaTransmisionTicket, cancellationToken);
        if (transmisionTicket.IdTipoMensaje == TipoMensaje.Exito)
        {
            idTransmisionSunat = transmisionTicket.Datos;
        }
        else
        {
            LogSiErrorSistema(transmisionTicket.IdTipoMensaje, transmisionTicket.Mensaje, idLoteDocumento, "falló al registrar la transmisión de resolución de ticket (getStatus)");
        }

        await GuardarCdrAsync(idInquilino, idLoteDocumento, idTransmisionSunat, carpeta, cabecera.Nombre, consulta.Datos.CdrXmlBytes!, cancellationToken);

        await loteRepositorio.ActualizarEstadoSunatAsync(
            UsuarioWorker, idInquilino, idLoteDocumento, consulta.Datos.EstadoCodigo, cabecera.Ticket,
            consulta.Datos.SunatCodigoRespuesta, consulta.Datos.SunatDescripcionRespuesta, cancellationToken);

        await itemRepositorio.ActualizarEstadoSunatTodosAsync(
            UsuarioWorker, idInquilino, idLoteDocumento, consulta.Datos.EstadoCodigo,
            consulta.Datos.SunatCodigoRespuesta, consulta.Datos.SunatDescripcionRespuesta, cancellationToken);

        var esAceptado = consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.ResumenBajaAceptado;
        var severidad = consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.ResumenBajaRechazado ? "Error" : "Advertencia";
        var ahora = RelojPeru.Ahora();

        await Task.WhenAll(lote.Datos.Items.Select(item => ProcesarItemBajaAsync(
            idInquilino, idLoteDocumento, item.IdDocumentoElectronico, consulta.Datos.EstadoCodigo,
            consulta.Datos.SunatCodigoRespuesta, consulta.Datos.SunatDescripcionRespuesta, ahora,
            esAceptado, severidad, carpeta, empresa.Datos, idTransmisionSunat, cancellationToken)));

        if (idTransmisionSunat is not null)
        {
            await transmisionRepositorio.ActualizarAsync(
                UsuarioWorker, idInquilino, idTransmisionSunat.Value,
                new ResultadoTransmisionSunat(
                    consulta.Datos.EstadoCodigo, consulta.Datos.SunatCodigoRespuesta, consulta.Datos.SunatDescripcionRespuesta, null, null),
                cancellationToken);
        }

        return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(consulta.Mensaje, consulta.Datos);
    }

    private async Task ProcesarItemBajaAsync(
        int idInquilino, int idLoteDocumento, int idDocumentoElectronico, EstadoMaestroCodigo estadoCodigo,
        string? sunatCodigoRespuesta, string? sunatDescripcionRespuesta, DateTime ahora, bool esAceptado,
        string severidad, string carpeta, Empresa empresa, int? idTransmisionSunat, CancellationToken cancellationToken)
    {
        var actualizarTask = documentoRepositorio.ActualizarEstadoSunatAsync(
            UsuarioWorker, idInquilino, idDocumentoElectronico, estadoCodigo,
            null, sunatCodigoRespuesta, sunatDescripcionRespuesta, null, ahora, cancellationToken);

        var siguienteTask = esAceptado
            ? RegenerarPdfAnuladoAsync(idInquilino, idLoteDocumento, idDocumentoElectronico, carpeta, empresa, idTransmisionSunat, cancellationToken)
            : errorRepositorio.InsertarAsync(
                UsuarioWorker, idInquilino,
                new ErrorDocumento(
                    idDocumentoElectronico, null, "Sunat",
                    sunatCodigoRespuesta ?? string.Empty, sunatDescripcionRespuesta ?? string.Empty,
                    null, severidad),
                cancellationToken);

        await Task.WhenAll(actualizarTask, siguienteTask);
    }

    private async Task GuardarCdrAsync(
        int idInquilino, int idLoteDocumento, int? idTransmisionSunat, string carpeta, string nombreLote, byte[] cdrXmlBytes, CancellationToken cancellationToken)
    {
        var nombreArchivo = $"{nombreLote}-{DateTime.UtcNow:yyyyMMddHHmmss}.cdr";
        var ruta = await almacenamiento.GuardarAsync(carpeta, nombreArchivo, cdrXmlBytes, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(cdrXmlBytes)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(null, idLoteDocumento, idTransmisionSunat, "Cdr", nombreArchivo, ruta, "application/xml", hash, cdrXmlBytes.LongLength);
        var resultado = await archivoRepositorio.InsertarAsync(UsuarioWorker, idInquilino, archivo, cancellationToken);
        LogSiErrorSistema(resultado.IdTipoMensaje, resultado.Mensaje, idLoteDocumento,
            $"se guardó {nombreArchivo} en S3 pero falló registrar el archivo en la base de datos");
    }

    private async Task RegenerarPdfAnuladoAsync(
        int idInquilino, int idLoteDocumento, int idDocumentoElectronico, string carpeta, Empresa empresa,
        int? idTransmisionSunat, CancellationToken cancellationToken)
    {
        var documentoTask = documentoRepositorio.ObtenerAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        var tokenPublicoTask = documentoRepositorio.ObtenerTokenPublicoAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        await Task.WhenAll(documentoTask, tokenPublicoTask);

        var documento = await documentoTask;
        if (documento.IdTipoMensaje != TipoMensaje.Exito || documento.Datos is null)
        {
            LogSiErrorSistema(documento.IdTipoMensaje, documento.Mensaje, idLoteDocumento,
                $"falló al obtener el documento {idDocumentoElectronico} para regenerar su Pdf anulado");
            return;
        }

        var tokenPublico = await tokenPublicoTask;
        if (tokenPublico.IdTipoMensaje != TipoMensaje.Exito || tokenPublico.Datos is null)
        {
            LogSiErrorSistema(tokenPublico.IdTipoMensaje, tokenPublico.Mensaje, idLoteDocumento,
                $"falló al obtener el token público del documento {idDocumentoElectronico} para regenerar su Pdf anulado");
            return;
        }

        var pdfBytes = generadorPdf.Construir(documento.Datos, empresa, tokenPublico.Datos, documento.Datos.Cabecera.SunatHash, anulado: true);

        var nombreArchivo = $"{documento.Datos.Cabecera.Serie}-{documento.Datos.Cabecera.Correlativo}-{DateTime.UtcNow:yyyyMMddHHmmss}-anulado.pdf";
        var ruta = await almacenamiento.GuardarAsync(carpeta, nombreArchivo, pdfBytes, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(
            idDocumentoElectronico, idLoteDocumento, idTransmisionSunat, "Pdf", nombreArchivo, ruta, "application/pdf", hash, pdfBytes.LongLength);
        var resultado = await archivoRepositorio.InsertarAsync(UsuarioWorker, idInquilino, archivo, cancellationToken);
        LogSiErrorSistema(resultado.IdTipoMensaje, resultado.Mensaje, idLoteDocumento,
            $"se guardó el Pdf anulado de {nombreArchivo} en S3 pero falló registrar el archivo en la base de datos");
    }
}
