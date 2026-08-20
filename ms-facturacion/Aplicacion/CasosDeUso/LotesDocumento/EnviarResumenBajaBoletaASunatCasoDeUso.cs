using System.Security.Cryptography;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;

/// Crea el Resumen Diario de Baja de Boletas y lo envía a SUNAT en el mismo paso — mismo esqueleto que
/// EnviarComunicacionBajaASunatCasoDeUso (sendSummary también termina en un ticket, nunca en un resultado
/// final), cambiando el constructor de XML inyectado (SummaryDocuments/"RC-" en vez de VoidedDocuments/
/// "RA-") y los estados de destino (ResumenBaja* en vez de ComunicacionBaja*). Depende solo de Puertos.
public sealed class EnviarResumenBajaBoletaASunatCasoDeUso(
    ILoteDocumentoRepositorio loteRepositorio,
    IDocumentoElectronicoRepositorio documentoRepositorio,
    IEmpresaRepositorio empresaRepositorio,
    IConfiguracionFacturacionEmpresaRepositorio configuracionRepositorio,
    ICredencialInquilinoRepositorio credencialRepositorio,
    ICifradoInquilinoServicio cifradoServicio,
    IConstructorXmlResumenBajaBoletaServicio constructorXml,
    IFirmadorXmlServicio firmador,
    IProveedorCertificadoServicio proveedorCertificado,
    IEmpaquetadorZipServicio empaquetador,
    IAlmacenamientoArchivosServicio almacenamiento,
    IArchivoDocumentoRepositorio archivoRepositorio,
    ITransmisionSunatRepositorio transmisionRepositorio,
    ISunatSummaryServiceCliente sunatCliente,
    IItemLoteDocumentoRepositorio itemRepositorio,
    IErrorDocumentoRepositorio errorRepositorio,
    ILogger<EnviarResumenBajaBoletaASunatCasoDeUso> logger)
{
    private const string UsuarioWorker = "ms-facturacion-worker";

    public async Task<ResultadoOperacion<LoteDocumentoCreado>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly fechaReferencia, IReadOnlyList<ItemBajaEntrada> items,
        string ambienteCodigo, CancellationToken cancellationToken)
    {
        try
        {
            return await EjecutarInternoAsync(idInquilino, idEmpresa, fechaReferencia, items, ambienteCodigo, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "EnviarResumenBajaBoleta — excepción no controlada. idInquilino={IdInquilino}, idEmpresa={IdEmpresa}, ambienteCodigo={AmbienteCodigo}.",
                idInquilino, idEmpresa, ambienteCodigo);

            return ResultadoOperacion<LoteDocumentoCreado>.DeErrorSistema(ex.Message);
        }
    }

    private void LogSiErrorSistema(TipoMensaje idTipoMensaje, string mensaje, int idLoteDocumento, string contexto)
    {
        if (idTipoMensaje == TipoMensaje.ErrorSistema)
        {
            logger.LogError(
                "EnviarResumenBajaBoleta — {Contexto}. idLoteDocumento={IdLoteDocumento}: {Mensaje}",
                contexto, idLoteDocumento, mensaje);
        }
    }

    private async Task<ResultadoOperacion<LoteDocumentoCreado>> EjecutarInternoAsync(
        int idInquilino, int idEmpresa, DateOnly fechaReferencia, IReadOnlyList<ItemBajaEntrada> items,
        string ambienteCodigo, CancellationToken cancellationToken)
    {
        var fechaGeneracion = DateOnly.FromDateTime(RelojPeru.Ahora());
        var creado = await loteRepositorio.InsertarResumenBajaBoletaAsync(UsuarioWorker, idInquilino, idEmpresa, fechaReferencia, fechaGeneracion, items, cancellationToken);
        if (creado.IdTipoMensaje != TipoMensaje.Exito || creado.Datos is null)
        {
            LogSiErrorSistema(creado.IdTipoMensaje, creado.Mensaje, 0, "falló al insertar el lote");
            return new ResultadoOperacion<LoteDocumentoCreado>(creado.IdTipoMensaje, creado.Mensaje, default);
        }

        var loteTask = loteRepositorio.ObtenerAsync(idInquilino, creado.Datos.IdLoteDocumento, cancellationToken);
        var empresaTask = empresaRepositorio.ObtenerAsync(idInquilino, idEmpresa, cancellationToken);
        var configuracionTask = configuracionRepositorio.ObtenerPorEmpresaYAmbienteAsync(idInquilino, idEmpresa, ambienteCodigo, cancellationToken);
        var claveSolTask = credencialRepositorio.ObtenerPorTipoAsync(idInquilino, idEmpresa, "ClaveSol", cancellationToken);

        await Task.WhenAll(loteTask, empresaTask, configuracionTask, claveSolTask);

        var lote = await loteTask;
        if (lote.IdTipoMensaje != TipoMensaje.Exito || lote.Datos is null)
        {
            LogSiErrorSistema(lote.IdTipoMensaje, lote.Mensaje, creado.Datos.IdLoteDocumento, "falló al obtener el lote recién creado");
            return new ResultadoOperacion<LoteDocumentoCreado>(lote.IdTipoMensaje, lote.Mensaje, default);
        }

        var empresa = await empresaTask;
        if (empresa.IdTipoMensaje != TipoMensaje.Exito || empresa.Datos is null)
        {
            LogSiErrorSistema(empresa.IdTipoMensaje, empresa.Mensaje, creado.Datos.IdLoteDocumento, "falló al obtener la empresa");
            return new ResultadoOperacion<LoteDocumentoCreado>(empresa.IdTipoMensaje, empresa.Mensaje, default);
        }

        var configuracion = await configuracionTask;
        if (configuracion.IdTipoMensaje != TipoMensaje.Exito || configuracion.Datos is null)
        {
            LogSiErrorSistema(configuracion.IdTipoMensaje, configuracion.Mensaje, creado.Datos.IdLoteDocumento, "falló al obtener la configuración de facturación");
            return new ResultadoOperacion<LoteDocumentoCreado>(configuracion.IdTipoMensaje, configuracion.Mensaje, default);
        }

        if (string.IsNullOrWhiteSpace(configuracion.Datos.UrlEnvioFacturaBoletaNota))
        {
            return ResultadoOperacion<LoteDocumentoCreado>.DeReglaDeNegocio(
                "La configuración de facturación de la empresa no tiene URL de envío (billService).");
        }

        var certificado = await proveedorCertificado.ObtenerAsync(idInquilino, idEmpresa, configuracion.Datos.IdCertificado, cancellationToken);
        if (certificado.IdTipoMensaje != TipoMensaje.Exito || certificado.Datos is null)
        {
            LogSiErrorSistema(certificado.IdTipoMensaje, certificado.Mensaje, creado.Datos.IdLoteDocumento, "falló al obtener/cargar el certificado");
            return new ResultadoOperacion<LoteDocumentoCreado>(certificado.IdTipoMensaje, certificado.Mensaje, default);
        }

        var claveSol = await claveSolTask;
        if (claveSol.IdTipoMensaje != TipoMensaje.Exito || claveSol.Datos is null)
        {
            LogSiErrorSistema(claveSol.IdTipoMensaje, claveSol.Mensaje, creado.Datos.IdLoteDocumento, "falló al obtener la credencial ClaveSol");
            return new ResultadoOperacion<LoteDocumentoCreado>(claveSol.IdTipoMensaje, claveSol.Mensaje, default);
        }

        var claveSolDescifrada = await cifradoServicio.DescifrarAsync(
            idInquilino, claveSol.Datos.ValorCifrado, claveSol.Datos.Nonce, claveSol.Datos.Tag, cancellationToken);
        if (claveSolDescifrada.IdTipoMensaje != TipoMensaje.Exito || claveSolDescifrada.Datos is null)
        {
            LogSiErrorSistema(claveSolDescifrada.IdTipoMensaje, claveSolDescifrada.Mensaje, creado.Datos.IdLoteDocumento, "falló al descifrar la ClaveSol");
            return new ResultadoOperacion<LoteDocumentoCreado>(claveSolDescifrada.IdTipoMensaje, claveSolDescifrada.Mensaje, default);
        }

        var xmlSinFirmar = constructorXml.Construir(lote.Datos, empresa.Datos);
        var xmlFirmado = firmador.Firmar(xmlSinFirmar, certificado.Datos);

        var nombreBase = $"{empresa.Datos.Ruc}-{lote.Datos.Cabecera.Nombre}";
        var nombreArchivoXml = $"{nombreBase}.xml";
        var nombreArchivoZip = $"{nombreBase}.zip";
        var zipBytes = empaquetador.Empaquetar(nombreArchivoXml, xmlFirmado);

        var idLoteDocumento = lote.Datos.Cabecera.IdLoteDocumento;
        var carpeta = $"{idInquilino}/{idEmpresa}/{fechaReferencia:yyyy}/{fechaReferencia:MM}/resumen-baja-{lote.Datos.Cabecera.Nombre}";
        var nombreAlmacenamiento = $"{lote.Datos.Cabecera.Nombre}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var usuarioSolCompleto = empresa.Datos.Ruc + claveSol.Datos.Usuario;

        var nuevaTransmision = new NuevaTransmisionSunat(
            null, idLoteDocumento, configuracion.Datos.TipoProveedorCodigo, configuracion.Datos.UrlEnvioFacturaBoletaNota,
            "sendSummary", 1);

        var transmision = await transmisionRepositorio.InsertarAsync(UsuarioWorker, idInquilino, nuevaTransmision, cancellationToken);
        if (transmision.IdTipoMensaje != TipoMensaje.Exito)
        {
            LogSiErrorSistema(transmision.IdTipoMensaje, transmision.Mensaje, idLoteDocumento, "falló al registrar el intento de transmisión");
            return new ResultadoOperacion<LoteDocumentoCreado>(transmision.IdTipoMensaje, transmision.Mensaje, default);
        }

        var archivoXmlTask = GuardarArchivoAsync(idInquilino, idLoteDocumento, transmision.Datos, carpeta, $"{nombreAlmacenamiento}.xml", xmlFirmado, "Xml", "application/xml", cancellationToken);
        var archivoZipTask = GuardarArchivoAsync(idInquilino, idLoteDocumento, transmision.Datos, carpeta, $"{nombreAlmacenamiento}.zip", zipBytes, "Zip", "application/zip", cancellationToken);

        await Task.WhenAll(archivoXmlTask, archivoZipTask);

        var envio = await sunatCliente.EnviarAsync(
            configuracion.Datos.UrlEnvioFacturaBoletaNota, usuarioSolCompleto, claveSolDescifrada.Datos, nombreArchivoZip, zipBytes, cancellationToken);

        if (envio.IdTipoMensaje != TipoMensaje.Exito || envio.Datos is null)
        {
            LogSiErrorSistema(envio.IdTipoMensaje, envio.Mensaje, idLoteDocumento, "sendSummary falló");

            await transmisionRepositorio.ActualizarAsync(
                UsuarioWorker, idInquilino, transmision.Datos,
                new ResultadoTransmisionSunat(EstadoMaestroCodigo.ErrorSunat, null, null, envio.IdTipoMensaje.ToString(), envio.Mensaje),
                cancellationToken);

            if (envio.IdTipoMensaje == TipoMensaje.ReglaDeNegocio)
            {
                await loteRepositorio.ActualizarEstadoSunatAsync(
                    UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.ResumenBajaConError, null, envio.IdTipoMensaje.ToString(), envio.Mensaje, cancellationToken);
                await itemRepositorio.ActualizarEstadoSunatTodosAsync(
                    UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.ResumenBajaConError, null, null, cancellationToken);

                var ahoraError = RelojPeru.Ahora();
                await Task.WhenAll(lote.Datos.Items.Select(item => Task.WhenAll(
                    documentoRepositorio.ActualizarEstadoSunatAsync(
                        UsuarioWorker, idInquilino, item.IdDocumentoElectronico, EstadoMaestroCodigo.ResumenBajaConError,
                        null, null, null, null, ahoraError, cancellationToken),
                    errorRepositorio.InsertarAsync(
                        UsuarioWorker, idInquilino,
                        new ErrorDocumento(item.IdDocumentoElectronico, transmision.Datos, "Sunat", string.Empty, envio.Mensaje, null, "Error"),
                        cancellationToken))));
            }

            return new ResultadoOperacion<LoteDocumentoCreado>(envio.IdTipoMensaje, envio.Mensaje, default);
        }

        await transmisionRepositorio.ActualizarAsync(
            UsuarioWorker, idInquilino, transmision.Datos,
            new ResultadoTransmisionSunat(EstadoMaestroCodigo.TicketRecibido, null, null, null, null),
            cancellationToken);

        await loteRepositorio.ActualizarEstadoSunatAsync(
            UsuarioWorker, idInquilino, lote.Datos.Cabecera.IdLoteDocumento, EstadoMaestroCodigo.TicketRecibido, envio.Datos, null, null, cancellationToken);

        var fechaActualizacion = RelojPeru.Ahora();
        await Task.WhenAll(lote.Datos.Items.Select(item =>
            documentoRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, item.IdDocumentoElectronico, EstadoMaestroCodigo.ResumenBajaEnviado,
                null, null, null, null, fechaActualizacion, cancellationToken)));

        var resultado = new LoteDocumentoCreado(
            lote.Datos.Cabecera.IdLoteDocumento, lote.Datos.Cabecera.Nombre, "TicketRecibido", lote.Datos.Cabecera.FechaGeneracion);

        return ResultadoOperacion<LoteDocumentoCreado>.DeExito($"Resumen de baja enviado, ticket: {envio.Datos}.", resultado);
    }

    private async Task GuardarArchivoAsync(
        int idInquilino, int idLoteDocumento, int idTransmisionSunat, string carpeta, string nombreArchivo, byte[] contenido, string tipoArchivoCodigo,
        string tipoContenido, CancellationToken cancellationToken)
    {
        var ruta = await almacenamiento.GuardarAsync(carpeta, nombreArchivo, contenido, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(null, idLoteDocumento, idTransmisionSunat, tipoArchivoCodigo, nombreArchivo, ruta, tipoContenido, hash, contenido.LongLength);
        var resultado = await archivoRepositorio.InsertarAsync(UsuarioWorker, idInquilino, archivo, cancellationToken);

        LogSiErrorSistema(resultado.IdTipoMensaje, resultado.Mensaje, idLoteDocumento,
            $"se guardó {nombreArchivo} en S3 pero falló registrar el archivo en la base de datos");
    }
}
