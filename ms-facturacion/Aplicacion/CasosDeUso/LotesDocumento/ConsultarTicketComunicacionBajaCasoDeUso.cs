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
    ITransmisionSunatRepositorio transmisionRepositorio,
    ISunatSummaryServiceCliente sunatCliente,
    IErrorDocumentoRepositorio errorRepositorio,
    IGeneradorPdfComprobanteServicio generadorPdf,
    ILogger<ConsultarTicketComunicacionBajaCasoDeUso> logger)
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
            // Mismo criterio que EnviarDocumentoElectronicoASunatCasoDeUso/EnviarComunicacionBajaASunatCasoDeUso.
            logger.LogError(
                ex, "ConsultarTicketComunicacionBaja — excepción no controlada. idInquilino={IdInquilino}, idLoteDocumento={IdLoteDocumento}, ambienteCodigo={AmbienteCodigo}.",
                idInquilino, idLoteDocumento, ambienteCodigo);

            return ResultadoOperacion<ResultadoConsultaTicket>.DeErrorSistema(ex.Message);
        }
    }

    private void LogSiErrorSistema(TipoMensaje idTipoMensaje, string mensaje, int idLoteDocumento, string contexto)
    {
        if (idTipoMensaje == TipoMensaje.ErrorSistema)
        {
            logger.LogError(
                "ConsultarTicketComunicacionBaja — {Contexto}. idLoteDocumento={IdLoteDocumento}: {Mensaje}",
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

        // empresa/configuracion/claveSol no dependen entre sí — mismo criterio que EnviarDocumentoElectronico
        // ASunatCasoDeUso/EnviarComunicacionBajaASunatCasoDeUso.
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

        // 98: todavía en proceso — solo se refleja el estado, no hay CDR que interpretar aún.
        if (consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.TicketPendiente)
        {
            await loteRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketPendiente, cabecera.Ticket, null, null, cancellationToken);

            return ResultadoOperacion<ResultadoConsultaTicket>.DeExito(consulta.Mensaje, consulta.Datos);
        }

        // 99: error técnico de SUNAT al procesar el ticket, sin CDR utilizable. El documento había quedado
        // en ComunicacionBajaEnviada al enviar la baja (ver EnviarComunicacionBajaASunatCasoDeUso) — sin
        // este paso se quedaría ahí para siempre, mostrando "en curso" indefinidamente aunque el intento
        // ya terminó en error. ComunicacionBajaConError (no el genérico Error) porque este SP ahora separa
        // EstadoAnulacionCodigo de EstadoCodigo — Error significa "la emisión nunca llegó a SUNAT", algo
        // completamente distinto de "la baja de un documento ya aceptado falló al consultar el ticket".
        if (consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.TicketConError)
        {
            await loteRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, cabecera.Ticket, null, null, cancellationToken);
            await itemRepositorio.ActualizarEstadoSunatTodosAsync(
                UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, null, null, cancellationToken);

            // Cada item es un IdDocumentoElectronico distinto (filas independientes) — se procesan todos en
            // paralelo; dentro de cada item, el update de estado y el insert de error tampoco dependen entre sí.
            var ahoraError = RelojPeru.Ahora();
            await Task.WhenAll(lote.Datos.Items.Select(item => Task.WhenAll(
                documentoRepositorio.ActualizarEstadoSunatAsync(
                    UsuarioWorker, idInquilino, item.IdDocumentoElectronico, EstadoMaestroCodigo.ComunicacionBajaConError,
                    null, null, null, null, ahoraError, cancellationToken),
                // Mismo criterio que el branch de abajo (statusCode 0, Rechazada) — ComunicacionBajaConError
                // es un fallo real (el ticket nunca se resolvió), no debía quedar sin registro en
                // ERRORES_DOCUMENTO solo porque no vino de un CDR.
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
        var carpeta = $"{idInquilino}/{cabecera.IdEmpresa}/{cabecera.FechaReferencia:yyyy}/{cabecera.FechaReferencia:MM}/baja-{cabecera.Nombre}";

        // getStatus es una llamada real a SUNAT que hasta ahora nunca quedaba registrada en
        // TRANSMISIONES_SUNAT — se registra acá (mismo criterio "transmisión antes que sus archivos" que
        // EnviarDocumentoElectronicoASunatCasoDeUso/EnviarComunicacionBajaASunatCasoDeUso) para que el CDR y
        // el Pdf "ANULADO" regenerado por cada item se vinculen a ella en vez de quedar sin transmisión. Un
        // fallo acá no debe bloquear la actualización real de lote/items/documentos — solo se loguea y se
        // sigue con idTransmisionSunat = null.
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

        var esAceptado = consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.ComunicacionBajaAceptada;
        var severidad = consulta.Datos.EstadoCodigo == EstadoMaestroCodigo.ComunicacionBajaRechazada ? "Error" : "Advertencia";
        var ahora = RelojPeru.Ahora();

        // Cada item es un IdDocumentoElectronico distinto — se procesan todos en paralelo en vez de uno
        // detrás de otro (mismo criterio que los dos branches de arriba).
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

    // El update de estado y el siguiente paso (insertar error o regenerar el Pdf) tampoco dependen entre sí.
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

    // Regenera el PDF del documento ya anulado con la marca de agua "ANULADO" y lo guarda como una fila
    // nueva de ARCHIVOS_DOCUMENTO — con IdDocumentoElectronico (para que SP_ArchivoDocumento_ObtenerXmlYPdf
    // lo encuentre directo, sin ambigüedad si el lote incluyó más de un documento) e IdLoteDocumento (solo
    // como rastro de auditoría de qué baja lo generó, no se usa para la búsqueda). Falla en silencio — el
    // documento ya quedó correctamente marcado ComunicacionBajaAceptada antes de llegar acá, así que un
    // problema al regenerar el PDF no debe tirar abajo el resultado real de la baja.
    private async Task RegenerarPdfAnuladoAsync(
        int idInquilino, int idLoteDocumento, int idDocumentoElectronico, string carpeta, Empresa empresa,
        int? idTransmisionSunat, CancellationToken cancellationToken)
    {
        // documento y tokenPublico no dependen entre sí — ambos solo necesitan idInquilino/idDocumentoElectronico.
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
