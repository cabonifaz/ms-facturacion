using System.Security.Cryptography;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Marca un documento como AnuladoManualmente y, si eso tuvo éxito, registra un lote+transmisión "Manual" y
/// regenera el Pdf con la marca de agua "ANULADO" (mismo patrón que
/// ConsultarTicketComunicacionBajaCasoDeUso.RegenerarPdfAnuladoAsync para ComunicacionBajaAceptada). Depende
/// solo de Puertos.
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
    public async Task<ResultadoOperacion<EstadoDocumentoElectronicoActualizado>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, string motivo, DateTime fechaAnulacion,
        CancellationToken cancellationToken)
    {
        var resultado = await documentoRepositorio.AnularManualmenteAsync(
            usuarioEjecutor, idInquilino, idDocumentoElectronico, motivo, fechaAnulacion, cancellationToken);

        if (resultado.IdTipoMensaje != TipoMensaje.Exito)
        {
            return resultado;
        }

        // El documento ya quedó marcado AnuladoManualmente en la línea de arriba — un problema en
        // cualquiera de estos pasos (lote/transmisión/Pdf) no debe tirar abajo ese resultado real, mismo
        // criterio que RegenerarPdfAnuladoAsync.
        try
        {
            await RegistrarLoteYPdfAsync(usuarioEjecutor, idInquilino, idDocumentoElectronico, motivo, fechaAnulacion, cancellationToken);
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
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, string motivo, DateTime fechaAnulacion,
        CancellationToken cancellationToken)
    {
        var documentoTask = documentoRepositorio.ObtenerAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        var tokenPublicoTask = documentoRepositorio.ObtenerTokenPublicoAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        await Task.WhenAll(documentoTask, tokenPublicoTask);

        var documento = await documentoTask;
        if (documento.IdTipoMensaje != TipoMensaje.Exito || documento.Datos is null)
        {
            LogSiErrorSistema(documento.IdTipoMensaje, documento.Mensaje, idDocumentoElectronico, "falló al obtener el documento para regenerar su Pdf anulado");
            return;
        }

        var tokenPublico = await tokenPublicoTask;
        if (tokenPublico.IdTipoMensaje != TipoMensaje.Exito || tokenPublico.Datos is null)
        {
            LogSiErrorSistema(tokenPublico.IdTipoMensaje, tokenPublico.Mensaje, idDocumentoElectronico, "falló al obtener el token público para regenerar su Pdf anulado");
            return;
        }

        var cabecera = documento.Datos.Cabecera;

        var empresa = await empresaRepositorio.ObtenerAsync(idInquilino, cabecera.IdEmpresa, cancellationToken);
        if (empresa.IdTipoMensaje != TipoMensaje.Exito || empresa.Datos is null)
        {
            LogSiErrorSistema(empresa.IdTipoMensaje, empresa.Mensaje, idDocumentoElectronico, "falló al obtener la empresa para regenerar su Pdf anulado");
            return;
        }

        var lote = await loteRepositorio.InsertarManualAsync(
            usuarioEjecutor, idInquilino, cabecera.IdEmpresa, idDocumentoElectronico, motivo,
            cabecera.FechaEmision, fechaAnulacion, cancellationToken);
        if (lote.IdTipoMensaje != TipoMensaje.Exito || lote.Datos is null)
        {
            LogSiErrorSistema(lote.IdTipoMensaje, lote.Mensaje, idDocumentoElectronico, "falló al registrar el lote de anulación manual");
            return;
        }

        // Metodo='Manual': no hay una transmisión SOAP real a SUNAT detrás de esto (a diferencia de
        // sendBill/sendSummary), pero se registra igual para que el Pdf tenga un IdTransmisionSunat propio
        // en vez de depender de "el más reciente por FchCre" — ver SP_ArchivoDocumento_ObtenerXmlYPdf.
        var nuevaTransmision = new NuevaTransmisionSunat(
            null, lote.Datos.IdLoteDocumento, "Manual", "N/A (anulación registrada manualmente)", "Manual", 1);
        var transmision = await transmisionRepositorio.InsertarAsync(usuarioEjecutor, idInquilino, nuevaTransmision, cancellationToken);
        if (transmision.IdTipoMensaje != TipoMensaje.Exito)
        {
            LogSiErrorSistema(transmision.IdTipoMensaje, transmision.Mensaje, idDocumentoElectronico, "falló al registrar la transmisión manual");
            return;
        }

        var pdfBytes = generadorPdf.Construir(documento.Datos, empresa.Datos, tokenPublico.Datos, cabecera.SunatHash, anulado: true);

        var carpeta = $"{idInquilino}/{cabecera.IdEmpresa}/{cabecera.FechaEmision:yyyy}/{cabecera.FechaEmision:MM}/{cabecera.Serie}-{cabecera.Correlativo}";
        var nombreArchivo = $"{cabecera.Serie}-{cabecera.Correlativo}-{DateTime.UtcNow:yyyyMMddHHmmss}-anulado.pdf";
        var ruta = await almacenamiento.GuardarAsync(carpeta, nombreArchivo, pdfBytes, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(
            idDocumentoElectronico, lote.Datos.IdLoteDocumento, transmision.Datos, "Pdf", nombreArchivo, ruta, "application/pdf", hash, pdfBytes.LongLength);
        var archivoInsertado = await archivoRepositorio.InsertarAsync(usuarioEjecutor, idInquilino, archivo, cancellationToken);
        if (archivoInsertado.IdTipoMensaje != TipoMensaje.Exito)
        {
            LogSiErrorSistema(archivoInsertado.IdTipoMensaje, archivoInsertado.Mensaje, idDocumentoElectronico, "se guardó el Pdf anulado en S3 pero falló registrar el archivo en la base de datos");
            return;
        }

        var resultadoTransmision = new ResultadoTransmisionSunat(
            EstadoMaestroCodigo.AnuladoManualmente, null, null, null, null);
        var transmisionActualizada = await transmisionRepositorio.ActualizarAsync(
            usuarioEjecutor, idInquilino, transmision.Datos, resultadoTransmision, cancellationToken);
        LogSiErrorSistema(transmisionActualizada.IdTipoMensaje, transmisionActualizada.Mensaje, idDocumentoElectronico, "falló al vincular el Pdf anulado a la transmisión manual");
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
