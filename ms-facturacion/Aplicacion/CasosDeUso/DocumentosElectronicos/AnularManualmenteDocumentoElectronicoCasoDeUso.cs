using System.Security.Cryptography;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Marca un documento como AnuladoManualmente (arrastrando automáticamente sus Notas de Crédito/Débito
/// vigentes — ver IDocumentoElectronicoRepositorio.AnularManualmenteAsync) y, si eso tuvo éxito, registra un
/// solo lote+transmisión "Manual" para todos los documentos afectados y regenera el Pdf "ANULADO" de cada
/// uno (mismo patrón que ConsultarTicketComunicacionBajaCasoDeUso.RegenerarPdfAnuladoAsync para
/// ComunicacionBajaAceptada). Depende solo de Puertos.
public sealed class AnularManualmenteDocumentoElectronicoCasoDeUso(
    IDocumentoElectronicoRepositorio documentoRepositorio,
    IEmpresaRepositorio empresaRepositorio,
    IGeneradorPdfComprobanteServicio generadorPdf,
    IAlmacenamientoArchivosServicio almacenamiento,
    IArchivoDocumentoRepositorio archivoRepositorio,
    ILoteDocumentoRepositorio loteRepositorio,
    ITransmisionSunatRepositorio transmisionRepositorio,
    ILogger<AnularManualmenteDocumentoElectronicoCasoDeUso> logger)
{
    public async Task<ResultadoOperacion<IReadOnlyList<EstadoDocumentoElectronicoActualizado>>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, string motivo, DateTime fechaAnulacion,
        CancellationToken cancellationToken)
    {
        var resultado = await documentoRepositorio.AnularManualmenteAsync(
            usuarioEjecutor, idInquilino, idDocumentoElectronico, motivo, fechaAnulacion, cancellationToken);

        if (resultado.IdTipoMensaje != TipoMensaje.Exito || resultado.Datos is null)
        {
            return resultado;
        }

        // El/los documento(s) ya quedaron marcados AnuladoManualmente en la línea de arriba — un problema en
        // cualquiera de estos pasos (lote/transmisión/Pdf) no debe tirar abajo ese resultado real, mismo
        // criterio que RegenerarPdfAnuladoAsync.
        try
        {
            var idsAfectados = resultado.Datos.Select(a => a.IdDocumentoElectronico).ToList();
            await RegistrarLoteYPdfAsync(usuarioEjecutor, idInquilino, idsAfectados, motivo, fechaAnulacion, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "AnularManualmente — la anulación se registró correctamente pero falló el lote/transmisión/Pdf. idInquilino={IdInquilino}, idDocumentoElectronico={IdDocumentoElectronico}.",
                idInquilino, idDocumentoElectronico);
        }

        return resultado;
    }

    private async Task RegistrarLoteYPdfAsync(
        string usuarioEjecutor, int idInquilino, IReadOnlyList<int> idsAfectados, string motivo, DateTime fechaAnulacion,
        CancellationToken cancellationToken)
    {
        // Cada documento afectado (padre + Notas arrastradas) puede tener su propia Serie/Correlativo/
        // FechaEmision — se obtienen todos en paralelo, uno por id.
        var documentos = await Task.WhenAll(idsAfectados.Select(id => ObtenerConTokenAsync(idInquilino, id, cancellationToken)));
        var validos = documentos.Where(d => d is not null).Select(d => d!.Value).ToList();
        if (validos.Count == 0)
        {
            return;
        }

        // Se asume que toda Nota arrastrada pertenece a la misma empresa que la Factura/Boleta que la
        // origina (mismo supuesto implícito que ya hace el JOIN de SP_LoteDocumento_Insertar) — el lote es
        // una sola fila, necesita un único IdEmpresa; se usa el del primer documento afectado (el padre).
        var idEmpresa = validos[0].Documento.Datos!.Cabecera.IdEmpresa;

        var empresa = await empresaRepositorio.ObtenerAsync(idInquilino, idEmpresa, cancellationToken);
        if (empresa.IdTipoMensaje != TipoMensaje.Exito || empresa.Datos is null)
        {
            LogSiErrorSistema(empresa.IdTipoMensaje, empresa.Mensaje, idsAfectados[0], "falló al obtener la empresa para regenerar los Pdf anulados");
            return;
        }

        var items = validos.Select(v => new ItemBajaEntrada(v.IdDocumentoElectronico, motivo)).ToList();
        var lote = await loteRepositorio.InsertarManualAsync(
            usuarioEjecutor, idInquilino, idEmpresa, items, DateOnly.FromDateTime(fechaAnulacion), fechaAnulacion, cancellationToken);
        if (lote.IdTipoMensaje != TipoMensaje.Exito || lote.Datos is null)
        {
            LogSiErrorSistema(lote.IdTipoMensaje, lote.Mensaje, idsAfectados[0], "falló al registrar el lote de anulación manual");
            return;
        }

        // Metodo='Manual': no hay una transmisión SOAP real a SUNAT detrás de esto (a diferencia de
        // sendBill/sendSummary), pero se registra igual para que cada Pdf tenga un IdTransmisionSunat propio
        // en vez de depender de "el más reciente por FchCre" — ver SP_ArchivoDocumento_ObtenerXmlYPdf. Una
        // sola transmisión para todos los documentos del lote, no una por documento.
        var nuevaTransmision = new NuevaTransmisionSunat(
            null, lote.Datos.IdLoteDocumento, "Manual", "N/A (anulación registrada manualmente)", "Manual", 1);
        var transmision = await transmisionRepositorio.InsertarAsync(usuarioEjecutor, idInquilino, nuevaTransmision, cancellationToken);
        if (transmision.IdTipoMensaje != TipoMensaje.Exito)
        {
            LogSiErrorSistema(transmision.IdTipoMensaje, transmision.Mensaje, idsAfectados[0], "falló al registrar la transmisión manual");
            return;
        }

        await Task.WhenAll(validos.Select(v => RegenerarPdfAsync(
            usuarioEjecutor, idInquilino, v, empresa.Datos, lote.Datos.IdLoteDocumento, transmision.Datos, cancellationToken)));

        var resultadoTransmision = new ResultadoTransmisionSunat(EstadoMaestroCodigo.AnuladoManualmente, null, null, null, null);
        var transmisionActualizada = await transmisionRepositorio.ActualizarAsync(
            usuarioEjecutor, idInquilino, transmision.Datos, resultadoTransmision, cancellationToken);
        LogSiErrorSistema(transmisionActualizada.IdTipoMensaje, transmisionActualizada.Mensaje, idsAfectados[0], "falló al cerrar la transmisión manual");
    }

    private async Task<(int IdDocumentoElectronico, ResultadoOperacion<DocumentoElectronicoDetalle> Documento, ResultadoOperacion<string> TokenPublico)?> ObtenerConTokenAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var documentoTask = documentoRepositorio.ObtenerAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        var tokenPublicoTask = documentoRepositorio.ObtenerTokenPublicoAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        await Task.WhenAll(documentoTask, tokenPublicoTask);

        var documento = await documentoTask;
        var tokenPublico = await tokenPublicoTask;

        if (documento.IdTipoMensaje != TipoMensaje.Exito || documento.Datos is null)
        {
            LogSiErrorSistema(documento.IdTipoMensaje, documento.Mensaje, idDocumentoElectronico, "falló al obtener el documento para regenerar su Pdf anulado");
            return null;
        }

        if (tokenPublico.IdTipoMensaje != TipoMensaje.Exito || tokenPublico.Datos is null)
        {
            LogSiErrorSistema(tokenPublico.IdTipoMensaje, tokenPublico.Mensaje, idDocumentoElectronico, "falló al obtener el token público para regenerar su Pdf anulado");
            return null;
        }

        return (idDocumentoElectronico, documento, tokenPublico);
    }

    private async Task RegenerarPdfAsync(
        string usuarioEjecutor, int idInquilino,
        (int IdDocumentoElectronico, ResultadoOperacion<DocumentoElectronicoDetalle> Documento, ResultadoOperacion<string> TokenPublico) datos,
        Empresa empresa, int idLoteDocumento, int idTransmisionSunat, CancellationToken cancellationToken)
    {
        var cabecera = datos.Documento.Datos!.Cabecera;
        var pdfBytes = generadorPdf.Construir(datos.Documento.Datos, empresa, datos.TokenPublico.Datos!, cabecera.SunatHash, anulado: true);

        var carpeta = $"{idInquilino}/{cabecera.IdEmpresa}/{cabecera.FechaEmision:yyyy}/{cabecera.FechaEmision:MM}/{cabecera.Serie}-{cabecera.Correlativo}";
        var nombreArchivo = $"{cabecera.Serie}-{cabecera.Correlativo}-{DateTime.UtcNow:yyyyMMddHHmmss}-anulado.pdf";
        var ruta = await almacenamiento.GuardarAsync(carpeta, nombreArchivo, pdfBytes, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(
            datos.IdDocumentoElectronico, idLoteDocumento, idTransmisionSunat, "Pdf", nombreArchivo, ruta, "application/pdf", hash, pdfBytes.LongLength);
        var archivoInsertado = await archivoRepositorio.InsertarAsync(usuarioEjecutor, idInquilino, archivo, cancellationToken);
        LogSiErrorSistema(archivoInsertado.IdTipoMensaje, archivoInsertado.Mensaje, datos.IdDocumentoElectronico, "se guardó el Pdf anulado en S3 pero falló registrar el archivo en la base de datos");
    }

    private void LogSiErrorSistema(TipoMensaje idTipoMensaje, string mensaje, int idDocumentoElectronico, string contexto)
    {
        if (idTipoMensaje == TipoMensaje.ErrorSistema)
        {
            logger.LogError(
                "AnularManualmente — {Contexto}. idDocumentoElectronico={IdDocumentoElectronico}, mensaje={Mensaje}.",
                contexto, idDocumentoElectronico, mensaje);
        }
    }
}
