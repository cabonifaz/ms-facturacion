using System.Security.Cryptography;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.LotesDocumento;

/// Crea el lote de Comunicación de Baja y lo envía a SUNAT en el mismo paso (sendSummary siempre
/// termina en un ticket, nunca en un resultado final — a diferencia del sendBill síncrono, aquí el
/// ticket ES el resultado esperable de éxito). Depende solo de Puertos, nunca de otros Casos de Uso.
public sealed class EnviarComunicacionBajaASunatCasoDeUso(
    ILoteDocumentoRepositorio loteRepositorio,
    IDocumentoElectronicoRepositorio documentoRepositorio,
    IEmpresaRepositorio empresaRepositorio,
    IConfiguracionFacturacionEmpresaRepositorio configuracionRepositorio,
    ICredencialInquilinoRepositorio credencialRepositorio,
    ICifradoInquilinoServicio cifradoServicio,
    IConstructorXmlBajaServicio constructorXml,
    IFirmadorXmlServicio firmador,
    IProveedorCertificadoServicio proveedorCertificado,
    IEmpaquetadorZipServicio empaquetador,
    IAlmacenamientoArchivosServicio almacenamiento,
    IArchivoDocumentoRepositorio archivoRepositorio,
    ITransmisionSunatRepositorio transmisionRepositorio,
    ISunatSummaryServiceCliente sunatCliente,
    IItemLoteDocumentoRepositorio itemRepositorio,
    IErrorDocumentoRepositorio errorRepositorio,
    ILogger<EnviarComunicacionBajaASunatCasoDeUso> logger)
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
            // Mismo criterio que EnviarDocumentoElectronicoASunatCasoDeUso — sin esto, una excepción en
            // cualquier paso (armado de XML, firma, S3, HTTP a SUNAT, etc.) no quedaba registrada en ningún
            // lado.
            logger.LogError(
                ex, "EnviarComunicacionBaja — excepción no controlada. idInquilino={IdInquilino}, idEmpresa={IdEmpresa}, ambienteCodigo={AmbienteCodigo}.",
                idInquilino, idEmpresa, ambienteCodigo);

            return ResultadoOperacion<LoteDocumentoCreado>.DeErrorSistema(ex.Message);
        }
    }

    // Único punto de logging para resultados que NO vinieron de una excepción: solo id=3 (ErrorSistema).
    // id=1 (ReglaDeNegocio) nunca se loguea: son resultados esperables, no fallas — mismo criterio que
    // EnviarDocumentoElectronicoASunatCasoDeUso.
    private void LogSiErrorSistema(TipoMensaje idTipoMensaje, string mensaje, int idLoteDocumento, string contexto)
    {
        if (idTipoMensaje == TipoMensaje.ErrorSistema)
        {
            logger.LogError(
                "EnviarComunicacionBaja — {Contexto}. idLoteDocumento={IdLoteDocumento}: {Mensaje}",
                contexto, idLoteDocumento, mensaje);
        }
    }

    private async Task<ResultadoOperacion<LoteDocumentoCreado>> EjecutarInternoAsync(
        int idInquilino, int idEmpresa, DateOnly fechaReferencia, IReadOnlyList<ItemBajaEntrada> items,
        string ambienteCodigo, CancellationToken cancellationToken)
    {
        var fechaGeneracion = DateOnly.FromDateTime(RelojPeru.Ahora());
        var creado = await loteRepositorio.InsertarAsync(UsuarioWorker, idInquilino, idEmpresa, fechaReferencia, fechaGeneracion, items, cancellationToken);
        if (creado.IdTipoMensaje != TipoMensaje.Exito || creado.Datos is null)
        {
            LogSiErrorSistema(creado.IdTipoMensaje, creado.Mensaje, 0, "falló al insertar el lote");
            return new ResultadoOperacion<LoteDocumentoCreado>(creado.IdTipoMensaje, creado.Mensaje, default);
        }

        // lote/empresa/configuracion/claveSol no dependen entre sí — lote solo necesita idInquilino/
        // creado.Datos.IdLoteDocumento (ya conocido), no el resultado de ninguno de los otros tres. Mismo
        // criterio que EnviarDocumentoElectronicoASunatCasoDeUso: se disparan juntas en vez de una detrás
        // de otra. certificado sí depende de configuracion.Datos.IdCertificado, así que se queda secuencial
        // después.
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
        var carpeta = $"{idInquilino}/{idEmpresa}/{fechaReferencia:yyyy}/{fechaReferencia:MM}/baja-{lote.Datos.Cabecera.Nombre}";
        var nombreAlmacenamiento = $"{lote.Datos.Cabecera.Nombre}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        // Xml y Zip no dependen entre sí (mismo criterio que EnviarDocumentoElectronicoASunatCasoDeUso).
        var archivoXmlTask = GuardarArchivoAsync(idInquilino, idLoteDocumento, carpeta, $"{nombreAlmacenamiento}.xml", xmlFirmado, "Xml", "application/xml", cancellationToken);
        var archivoZipTask = GuardarArchivoAsync(idInquilino, idLoteDocumento, carpeta, $"{nombreAlmacenamiento}.zip", zipBytes, "Zip", "application/zip", cancellationToken);

        await Task.WhenAll(archivoXmlTask, archivoZipTask);
        var idArchivoXml = await archivoXmlTask;
        var idArchivoZip = await archivoZipTask;

        var usuarioSolCompleto = empresa.Datos.Ruc + claveSol.Datos.Usuario;

        var nuevaTransmision = new NuevaTransmisionSunat(
            null, idLoteDocumento, configuracion.Datos.TipoProveedorCodigo, configuracion.Datos.UrlEnvioFacturaBoletaNota,
            "sendSummary", idArchivoZip, 1, idArchivoXml);

        var transmision = await transmisionRepositorio.InsertarAsync(UsuarioWorker, idInquilino, nuevaTransmision, cancellationToken);
        if (transmision.IdTipoMensaje != TipoMensaje.Exito)
        {
            LogSiErrorSistema(transmision.IdTipoMensaje, transmision.Mensaje, idLoteDocumento, "falló al registrar el intento de transmisión");
            return new ResultadoOperacion<LoteDocumentoCreado>(transmision.IdTipoMensaje, transmision.Mensaje, default);
        }

        var envio = await sunatCliente.EnviarAsync(
            configuracion.Datos.UrlEnvioFacturaBoletaNota, usuarioSolCompleto, claveSolDescifrada.Datos, nombreArchivoZip, zipBytes, cancellationToken);

        if (envio.IdTipoMensaje != TipoMensaje.Exito || envio.Datos is null)
        {
            LogSiErrorSistema(envio.IdTipoMensaje, envio.Mensaje, idLoteDocumento, "sendSummary falló");

            await transmisionRepositorio.ActualizarAsync(
                UsuarioWorker, idInquilino, transmision.Datos,
                new ResultadoTransmisionSunat(EstadoMaestroCodigo.ErrorSunat, null, null, null, envio.IdTipoMensaje.ToString(), envio.Mensaje),
                cancellationToken);

            // ReglaDeNegocio = SUNAT sí respondió al sendSummary, solo que sin ticket usable — un fallo real,
            // igual que el branch de TicketConError en ConsultarTicketComunicacionBajaCasoDeUso: se marca el
            // lote, cada documento (ComunicacionBajaConError) y se deja un registro en ERRORES_DOCUMENTO.
            // ErrorSistema = nunca hubo respuesta de SUNAT (ver el catch de SunatSummaryServiceCliente.EnviarAsync)
            // — no es un hecho sobre el documento ni sobre la baja en sí, así que el lote se deja en
            // PendienteEnvio (su estado de inserción) en vez de TicketConError, y ningún documento se toca.
            if (envio.IdTipoMensaje == TipoMensaje.ReglaDeNegocio)
            {
                await loteRepositorio.ActualizarEstadoSunatAsync(
                    UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, null, envio.IdTipoMensaje.ToString(), envio.Mensaje, cancellationToken);
                await itemRepositorio.ActualizarEstadoSunatTodosAsync(
                    UsuarioWorker, idInquilino, idLoteDocumento, EstadoMaestroCodigo.TicketConError, null, null, cancellationToken);

                // Cada item afecta un IdDocumentoElectronico distinto (filas independientes) — se procesan
                // todos en paralelo en vez de uno detrás de otro; dentro de cada item, el update de estado y
                // el insert de error tampoco dependen entre sí.
                var ahoraError = RelojPeru.Ahora();
                await Task.WhenAll(lote.Datos.Items.Select(item => Task.WhenAll(
                    documentoRepositorio.ActualizarEstadoSunatAsync(
                        UsuarioWorker, idInquilino, item.IdDocumentoElectronico, EstadoMaestroCodigo.ComunicacionBajaConError,
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
            new ResultadoTransmisionSunat(EstadoMaestroCodigo.TicketRecibido, null, null, null, null, null),
            cancellationToken);

        await loteRepositorio.ActualizarEstadoSunatAsync(
            UsuarioWorker, idInquilino, lote.Datos.Cabecera.IdLoteDocumento, EstadoMaestroCodigo.TicketRecibido, envio.Datos, null, null, cancellationToken);

        // El lote ya refleja TicketRecibido, pero DOCUMENTOS_ELECTRONICOS.EstadoCodigo — lo que en
        // realidad lee el listado de facturas (SP_DocumentoElectronico_ListarParaPedidoFactura) — se
        // quedaba en Aceptado durante toda la ventana de espera de SUNAT, sin reflejar que hay una
        // Comunicación de Baja en curso. ComunicacionBajaEnviada existe en el catálogo (TABLA_MAESTRA
        // IdMaestro=1, Num1=6) justo para esto; faltaba aplicarla acá.
        var fechaActualizacion = RelojPeru.Ahora();
        await Task.WhenAll(lote.Datos.Items.Select(item =>
            documentoRepositorio.ActualizarEstadoSunatAsync(
                UsuarioWorker, idInquilino, item.IdDocumentoElectronico, EstadoMaestroCodigo.ComunicacionBajaEnviada,
                null, null, null, null, fechaActualizacion, cancellationToken)));

        var resultado = new LoteDocumentoCreado(
            lote.Datos.Cabecera.IdLoteDocumento, lote.Datos.Cabecera.Nombre, "TicketRecibido", lote.Datos.Cabecera.FechaGeneracion);

        return ResultadoOperacion<LoteDocumentoCreado>.DeExito($"Comunicación de baja enviada, ticket: {envio.Datos}.", resultado);
    }

    private async Task<int> GuardarArchivoAsync(
        int idInquilino, int idLoteDocumento, string carpeta, string nombreArchivo, byte[] contenido, string tipoArchivoCodigo,
        string tipoContenido, CancellationToken cancellationToken)
    {
        var ruta = await almacenamiento.GuardarAsync(carpeta, nombreArchivo, contenido, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(null, idLoteDocumento, tipoArchivoCodigo, nombreArchivo, ruta, tipoContenido, hash, contenido.LongLength);
        var resultado = await archivoRepositorio.InsertarAsync(UsuarioWorker, idInquilino, archivo, cancellationToken);

        // No se propaga como falla del envío completo (el archivo ya está en S3) — pero antes esto se perdía
        // en silencio, mismo criterio que EnviarDocumentoElectronicoASunatCasoDeUso.GuardarYRegistrarArchivoAsync.
        LogSiErrorSistema(resultado.IdTipoMensaje, resultado.Mensaje, idLoteDocumento,
            $"se guardó {nombreArchivo} en S3 pero falló registrar el archivo en la base de datos");

        return resultado.Datos;
    }
}
