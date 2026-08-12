using System.Security.Cryptography;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Orquesta el camino síncrono sendBill para Factura/Boleta (01/03) y Nota de Crédito/Débito (07/08):
/// construir XML, firmar, empaquetar, enviar a SUNAT, interpretar el CDR y reflejar el estado final.
/// Depende solo de Puertos (nunca de otros Casos de Uso, por AGENTS.md) — cada paso que ya tiene su propio
/// Caso de Uso (Obtener, Descifrar, ActualizarEstadoSunat) se resuelve aquí llamando directamente al
/// Puerto subyacente.
public sealed class EnviarDocumentoElectronicoASunatCasoDeUso(
    IDocumentoElectronicoRepositorio documentoRepositorio,
    IEmpresaRepositorio empresaRepositorio,
    IConfiguracionFacturacionEmpresaRepositorio configuracionRepositorio,
    ICredencialInquilinoRepositorio credencialRepositorio,
    ICifradoInquilinoServicio cifradoServicio,
    IConstructorXmlComprobanteServicio constructorXml,
    IGeneradorPdfComprobanteServicio generadorPdf,
    IFirmadorXmlServicio firmador,
    IProveedorCertificadoServicio proveedorCertificado,
    IEmpaquetadorZipServicio empaquetador,
    IAlmacenamientoArchivosServicio almacenamiento,
    IArchivoDocumentoRepositorio archivoRepositorio,
    ITransmisionSunatRepositorio transmisionRepositorio,
    ISunatBillServiceCliente sunatCliente,
    IErrorDocumentoRepositorio errorRepositorio,
    ILogger<EnviarDocumentoElectronicoASunatCasoDeUso> logger)
{
    private static readonly string[] TiposDocumentoSoportados = ["01", "03", "07", "08"];
    private const string UsuarioWorker = "ms-facturacion-worker";

    public async Task<ResultadoOperacion<ResultadoEnvioSunat>> EjecutarAsync(
        int idInquilino, int idDocumentoElectronico, string ambienteCodigo, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "EnviarASunat — inicio. idInquilino={IdInquilino}, idDocumentoElectronico={IdDocumentoElectronico}, ambienteCodigo={AmbienteCodigo}.",
            idInquilino, idDocumentoElectronico, ambienteCodigo);

        try
        {
            return await EjecutarInternoAsync(idInquilino, idDocumentoElectronico, ambienteCodigo, cancellationToken);
        }
        catch (Exception ex)
        {
            // Antes de esto, una excepción en cualquier paso (armado de XML, firma, S3, HTTP a SUNAT, etc.)
            // no quedaba registrada en ningún lado — no hay middleware de excepciones global en este proyecto
            // (Program.cs no tiene UseExceptionHandler) y ninguno de estos pasos loguea por su cuenta, así
            // que el único rastro era un 500 crudo sin detalle. Se loguea acá con el stack trace completo
            // (incluye InnerException, clave para diferenciar p.ej. una falla de TLS/DNS/certificado de una
            // de credenciales AWS al desplegar en un entorno distinto al de desarrollo).
            logger.LogError(
                ex, "EnviarASunat — excepción no controlada. idInquilino={IdInquilino}, idDocumentoElectronico={IdDocumentoElectronico}, ambienteCodigo={AmbienteCodigo}.",
                idInquilino, idDocumentoElectronico, ambienteCodigo);

            return ResultadoOperacion<ResultadoEnvioSunat>.DeErrorSistema(ex.Message);
        }
    }

    private async Task<ResultadoOperacion<ResultadoEnvioSunat>> EjecutarInternoAsync(
        int idInquilino, int idDocumentoElectronico, string ambienteCodigo, CancellationToken cancellationToken)
    {
        var documento = await documentoRepositorio.ObtenerAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        if (documento.IdTipoMensaje != TipoMensaje.Exito || documento.Datos is null)
        {
            logger.LogWarning(
                "EnviarASunat — falló al obtener el documento (paso 1/lectura inicial): {Mensaje}", documento.Mensaje);
            return new ResultadoOperacion<ResultadoEnvioSunat>(documento.IdTipoMensaje, documento.Mensaje, default);
        }

        var cabecera = documento.Datos.Cabecera;

        if (!TiposDocumentoSoportados.Contains(cabecera.TipoDocumentoCodigo))
        {
            logger.LogWarning(
                "EnviarASunat — tipo de documento no soportado: {TipoDocumentoCodigo}.", cabecera.TipoDocumentoCodigo);
            return ResultadoOperacion<ResultadoEnvioSunat>.DeReglaDeNegocio(
                "El Worker todavía no soporta el envío síncrono para este tipo de documento (solo Factura/Boleta/Nota de Crédito/Nota de Débito por ahora).");
        }

        if (cabecera.EstadoCodigo is not ("PendienteEnvio" or "Error"))
        {
            logger.LogWarning(
                "EnviarASunat — el documento ya no está en un estado enviable: {EstadoCodigo}.", cabecera.EstadoCodigo);
            return ResultadoOperacion<ResultadoEnvioSunat>.DeReglaDeNegocio(
                $"El documento ya fue procesado (estado actual: {cabecera.EstadoCodigo}).");
        }

        // DOCUMENTOS_ELECTRONICOS no persiste FormaPagoCodigo (ver ConstructorXmlComprobanteServicio): que
        // haya cuotas ya significa Crédito. Como ahora las cuotas/líneas se editan de a una después de
        // Guardar, el balance pudo quedar temporalmente desincronizado — se valida recién aquí, al confirmar.
        if (documento.Datos.Cuotas.Count > 0)
        {
            var totalCuotas = documento.Datos.Cuotas.Sum(c => c.Monto);
            if (Math.Round(totalCuotas, 2) != Math.Round(cabecera.TotalImporte, 2))
            {
                logger.LogWarning(
                    "EnviarASunat — cuotas ({TotalCuotas}) no coinciden con el total del documento ({TotalImporte}).",
                    totalCuotas, cabecera.TotalImporte);
                return ResultadoOperacion<ResultadoEnvioSunat>.DeReglaDeNegocio(
                    "La suma de las cuotas no coincide con el total del documento. Corrija las cuotas antes de confirmar con SUNAT.");
            }
        }

        // El borrador guarda una FechaEmision/HoraEmision inicial, pero la emisión real ocurre recién
        // ahora — se recalcula al momento de confirmar, no al guardar.
        var ahora = RelojPeru.Ahora();
        var actualizacionFecha = await documentoRepositorio.ActualizarFechaEmisionAsync(
            UsuarioWorker, idInquilino, idDocumentoElectronico,
            DateOnly.FromDateTime(ahora), TimeOnly.FromDateTime(ahora), cancellationToken);
        if (actualizacionFecha.IdTipoMensaje != TipoMensaje.Exito)
        {
            logger.LogWarning("EnviarASunat — falló al actualizar fecha/hora de emisión: {Mensaje}", actualizacionFecha.Mensaje);
            return new ResultadoOperacion<ResultadoEnvioSunat>(actualizacionFecha.IdTipoMensaje, actualizacionFecha.Mensaje, default);
        }

        documento = await documentoRepositorio.ObtenerAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        if (documento.IdTipoMensaje != TipoMensaje.Exito || documento.Datos is null)
        {
            logger.LogWarning("EnviarASunat — falló al releer el documento tras actualizar la fecha: {Mensaje}", documento.Mensaje);
            return new ResultadoOperacion<ResultadoEnvioSunat>(documento.IdTipoMensaje, documento.Mensaje, default);
        }
        cabecera = documento.Datos.Cabecera;

        var empresa = await empresaRepositorio.ObtenerAsync(idInquilino, cabecera.IdEmpresa, cancellationToken);
        if (empresa.IdTipoMensaje != TipoMensaje.Exito || empresa.Datos is null)
        {
            logger.LogWarning(
                "EnviarASunat — falló al obtener la empresa (idEmpresa={IdEmpresa}): {Mensaje}", cabecera.IdEmpresa, empresa.Mensaje);
            return new ResultadoOperacion<ResultadoEnvioSunat>(empresa.IdTipoMensaje, empresa.Mensaje, default);
        }

        var configuracion = await configuracionRepositorio.ObtenerPorEmpresaYAmbienteAsync(
            idInquilino, cabecera.IdEmpresa, ambienteCodigo, cancellationToken);
        if (configuracion.IdTipoMensaje != TipoMensaje.Exito || configuracion.Datos is null)
        {
            logger.LogWarning(
                "EnviarASunat — falló al obtener la configuración de facturación (idEmpresa={IdEmpresa}, ambienteCodigo={AmbienteCodigo}): {Mensaje}",
                cabecera.IdEmpresa, ambienteCodigo, configuracion.Mensaje);
            return new ResultadoOperacion<ResultadoEnvioSunat>(configuracion.IdTipoMensaje, configuracion.Mensaje, default);
        }

        if (string.IsNullOrWhiteSpace(configuracion.Datos.UrlEnvioFacturaBoletaNota))
        {
            logger.LogWarning(
                "EnviarASunat — la configuración de facturación (idEmpresa={IdEmpresa}, ambienteCodigo={AmbienteCodigo}) no tiene UrlEnvioFacturaBoletaNota.",
                cabecera.IdEmpresa, ambienteCodigo);
            return ResultadoOperacion<ResultadoEnvioSunat>.DeReglaDeNegocio(
                "La configuración de facturación de la empresa no tiene URL de envío de Factura/Boleta/Nota.");
        }

        var certificado = await proveedorCertificado.ObtenerAsync(
            idInquilino, cabecera.IdEmpresa, configuracion.Datos.IdCertificado, cancellationToken);
        if (certificado.IdTipoMensaje != TipoMensaje.Exito || certificado.Datos is null)
        {
            logger.LogWarning(
                "EnviarASunat — falló al obtener/cargar el certificado (idCertificado={IdCertificado}): {Mensaje}",
                configuracion.Datos.IdCertificado, certificado.Mensaje);
            return new ResultadoOperacion<ResultadoEnvioSunat>(certificado.IdTipoMensaje, certificado.Mensaje, default);
        }

        var claveSol = await credencialRepositorio.ObtenerPorTipoAsync(idInquilino, cabecera.IdEmpresa, "ClaveSol", cancellationToken);
        if (claveSol.IdTipoMensaje != TipoMensaje.Exito || claveSol.Datos is null)
        {
            logger.LogWarning("EnviarASunat — falló al obtener la credencial ClaveSol: {Mensaje}", claveSol.Mensaje);
            return new ResultadoOperacion<ResultadoEnvioSunat>(claveSol.IdTipoMensaje, claveSol.Mensaje, default);
        }

        var claveSolDescifrada = await cifradoServicio.DescifrarAsync(
            idInquilino, claveSol.Datos.ValorCifrado, claveSol.Datos.Nonce, claveSol.Datos.Tag, cancellationToken);
        if (claveSolDescifrada.IdTipoMensaje != TipoMensaje.Exito || claveSolDescifrada.Datos is null)
        {
            logger.LogWarning("EnviarASunat — falló al descifrar la ClaveSol: {Mensaje}", claveSolDescifrada.Mensaje);
            return new ResultadoOperacion<ResultadoEnvioSunat>(claveSolDescifrada.IdTipoMensaje, claveSolDescifrada.Mensaje, default);
        }

        logger.LogInformation(
            "EnviarASunat — construyendo y firmando XML. idDocumentoElectronico={IdDocumentoElectronico}, tipoDocumentoCodigo={TipoDocumentoCodigo}, serie={Serie}, correlativo={Correlativo}.",
            cabecera.IdDocumentoElectronico, cabecera.TipoDocumentoCodigo, cabecera.Serie, cabecera.Correlativo);

        var xmlSinFirmar = constructorXml.Construir(documento.Datos, empresa.Datos);
        var xmlFirmado = firmador.Firmar(xmlSinFirmar, certificado.Datos);

        // "Valor resumen" del QR (Anexo C, RS 113-2018/SUNAT) = ds:DigestValue del XML firmado — nunca se
        // guardaba en DOCUMENTOS_ELECTRONICOS.SunatHash hasta ahora, se extrae acá recién que existe.
        var sunatHash = ExtraerDigestValue(xmlFirmado);

        // nombreArchivoXml/nombreArchivoZip son el nombre que exige SUNAT (RUC-Tipo-Serie-Correlativo, ver
        // empaquetador.Empaquetar/sunatCliente.EnviarAsync abajo) — no confundir con nombreAlmacenamiento,
        // el nombre bajo el que se guarda en S3, que es un detalle nuestro y puede ser más simple.
        var nombreBase = $"{empresa.Datos.Ruc}-{cabecera.TipoDocumentoCodigo}-{cabecera.Serie}-{cabecera.Correlativo}";
        var nombreArchivoXml = $"{nombreBase}.xml";
        var nombreArchivoZip = $"{nombreBase}.zip";
        var zipBytes = empaquetador.Empaquetar(nombreArchivoXml, xmlFirmado);

        var carpeta = $"{idInquilino}/{cabecera.IdEmpresa}/{cabecera.FechaEmision:yyyy}/{cabecera.FechaEmision:MM}/{cabecera.Serie}-{cabecera.Correlativo}";

        // Timestamp al final: cada intento de envío recibe su propio nombre, así un reintento no sobreescribe
        // en S3 el XML/ZIP/CDR del intento anterior (misma clave = mismo objeto). Compartido entre los 3
        // archivos de este intento (xml/zip acá, cdr más abajo) para que se lean como un mismo conjunto.
        var nombreAlmacenamiento = $"{cabecera.Serie}-{cabecera.Correlativo}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var idArchivoXml = await GuardarYRegistrarArchivoAsync(
            idInquilino, cabecera.IdDocumentoElectronico, carpeta, $"{nombreAlmacenamiento}.xml", xmlFirmado, "Xml", "application/xml", cancellationToken);
        var idArchivoZip = await GuardarYRegistrarArchivoAsync(
            idInquilino, cabecera.IdDocumentoElectronico, carpeta, $"{nombreAlmacenamiento}.zip", zipBytes, "Zip", "application/zip", cancellationToken);

        var usuarioSolCompleto = empresa.Datos.Ruc + claveSol.Datos.Usuario;

        var nuevaTransmision = new NuevaTransmisionSunat(
            cabecera.IdDocumentoElectronico, null, configuracion.Datos.TipoProveedorCodigo,
            configuracion.Datos.UrlEnvioFacturaBoletaNota, "sendBill", idArchivoZip, 1, idArchivoXml);

        var transmision = await transmisionRepositorio.InsertarAsync(UsuarioWorker, idInquilino, nuevaTransmision, cancellationToken);
        if (transmision.IdTipoMensaje != TipoMensaje.Exito)
        {
            logger.LogWarning("EnviarASunat — falló al registrar el intento de transmisión: {Mensaje}", transmision.Mensaje);
            return new ResultadoOperacion<ResultadoEnvioSunat>(transmision.IdTipoMensaje, transmision.Mensaje, default);
        }

        logger.LogInformation(
            "EnviarASunat — llamando a sendBill. idDocumentoElectronico={IdDocumentoElectronico}, url={Url}, zipBytes={ZipBytes}.",
            cabecera.IdDocumentoElectronico, configuracion.Datos.UrlEnvioFacturaBoletaNota, zipBytes.Length);

        var envio = await sunatCliente.EnviarAsync(
            configuracion.Datos.UrlEnvioFacturaBoletaNota, usuarioSolCompleto, claveSolDescifrada.Datos, nombreArchivoZip, zipBytes, cancellationToken);

        if (envio.IdTipoMensaje != TipoMensaje.Exito || envio.Datos is null)
        {
            logger.LogWarning(
                "EnviarASunat — sendBill falló. idDocumentoElectronico={IdDocumentoElectronico}, idTipoMensaje={IdTipoMensaje}: {Mensaje}",
                cabecera.IdDocumentoElectronico, envio.IdTipoMensaje, envio.Mensaje);

            await transmisionRepositorio.ActualizarAsync(
                UsuarioWorker, idInquilino, transmision.Datos,
                new ResultadoTransmisionSunat(EstadoMaestroCodigo.Error, null, null, null, envio.IdTipoMensaje.ToString(), envio.Mensaje),
                cancellationToken);

            return new ResultadoOperacion<ResultadoEnvioSunat>(envio.IdTipoMensaje, envio.Mensaje, default);
        }

        logger.LogInformation(
            "EnviarASunat — sendBill respondió. idDocumentoElectronico={IdDocumentoElectronico}, estadoCodigo={EstadoCodigo}, sunatCodigoRespuesta={SunatCodigoRespuesta}.",
            cabecera.IdDocumentoElectronico, envio.Datos.EstadoCodigo, envio.Datos.SunatCodigoRespuesta);

        var idArchivoCdr = await GuardarYRegistrarArchivoAsync(
            idInquilino, cabecera.IdDocumentoElectronico, carpeta, $"{nombreAlmacenamiento}.cdr", envio.Datos.CdrXmlBytes, "Cdr", "application/xml", cancellationToken);

        int? idArchivoPdf = null;
        if (envio.Datos.EstadoCodigo is EstadoMaestroCodigo.Aceptado or EstadoMaestroCodigo.AceptadoConObservaciones)
        {
            var tokenPublico = await documentoRepositorio.ObtenerTokenPublicoAsync(idInquilino, cabecera.IdDocumentoElectronico, cancellationToken);
            if (tokenPublico.IdTipoMensaje == TipoMensaje.Exito && tokenPublico.Datos is not null)
            {
                var pdfBytes = generadorPdf.Construir(documento.Datos, empresa.Datos, tokenPublico.Datos, sunatHash);
                idArchivoPdf = await GuardarYRegistrarArchivoAsync(
                    idInquilino, cabecera.IdDocumentoElectronico, carpeta, $"{nombreAlmacenamiento}.pdf", pdfBytes, "Pdf", "application/pdf", cancellationToken);
            }
        }

        await transmisionRepositorio.ActualizarAsync(
            UsuarioWorker, idInquilino, transmision.Datos,
            new ResultadoTransmisionSunat(
                envio.Datos.EstadoCodigo, idArchivoCdr, envio.Datos.SunatCodigoRespuesta, envio.Datos.SunatDescripcionRespuesta, null, null, idArchivoPdf),
            cancellationToken);

        if (envio.Datos.EstadoCodigo != EstadoMaestroCodigo.Aceptado)
        {
            var severidad = envio.Datos.EstadoCodigo == EstadoMaestroCodigo.Rechazado ? "Error" : "Advertencia";

            // Cuando el CDR trae observaciones (cbc:Note) se guarda una fila por cada una — antes solo se
            // guardaba la Description principal y el resto de observaciones se perdía. Si no hay Note (caso
            // típico de un Rechazado simple, sin lista de observaciones), se conserva el comportamiento
            // anterior: una sola fila con el código/descripción principal del Response.
            var mensajes = envio.Datos.Observaciones.Count > 0
                ? envio.Datos.Observaciones
                : [envio.Datos.SunatDescripcionRespuesta];

            foreach (var mensaje in mensajes)
            {
                await errorRepositorio.InsertarAsync(
                    UsuarioWorker, idInquilino,
                    new ErrorDocumento(
                        cabecera.IdDocumentoElectronico, transmision.Datos, "Sunat",
                        envio.Datos.SunatCodigoRespuesta, mensaje, null, severidad),
                    cancellationToken);
            }
        }

        await documentoRepositorio.ActualizarEstadoSunatAsync(
            UsuarioWorker, idInquilino, cabecera.IdDocumentoElectronico, envio.Datos.EstadoCodigo,
            sunatHash, envio.Datos.SunatCodigoRespuesta, envio.Datos.SunatDescripcionRespuesta, null,
            RelojPeru.Ahora(), cancellationToken);

        logger.LogInformation(
            "EnviarASunat — fin. idDocumentoElectronico={IdDocumentoElectronico}, estadoCodigo={EstadoCodigo}.",
            cabecera.IdDocumentoElectronico, envio.Datos.EstadoCodigo);

        return ResultadoOperacion<ResultadoEnvioSunat>.DeExito("Documento procesado por SUNAT.", envio.Datos);
    }

    private async Task<int?> GuardarYRegistrarArchivoAsync(
        int idInquilino, int idDocumentoElectronico, string carpeta, string nombreArchivo, byte[] contenido, string tipoArchivoCodigo,
        string tipoContenido, CancellationToken cancellationToken)
    {
        var ruta = await almacenamiento.GuardarAsync(carpeta, nombreArchivo, contenido, cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant();

        var archivo = new ArchivoDocumento(
            idDocumentoElectronico, null, tipoArchivoCodigo, nombreArchivo, ruta, tipoContenido, hash, contenido.LongLength);

        var resultado = await archivoRepositorio.InsertarAsync(UsuarioWorker, idInquilino, archivo, cancellationToken);
        if (resultado.IdTipoMensaje != TipoMensaje.Exito)
        {
            // No se propaga como falla del envío completo (el archivo ya está en S3, y el resto del flujo
            // puede seguir sin este registro) — pero antes esto se perdía en silencio: el envío a SUNAT
            // seguía como si nada, dejando ARCHIVOS_DOCUMENTO_ELECTRONICO desincronizado sin ningún rastro.
            logger.LogWarning(
                "EnviarASunat — se guardó {NombreArchivo} en S3 pero falló registrar el archivo en la base de datos: {Mensaje}",
                nombreArchivo, resultado.Mensaje);
        }

        return resultado.IdTipoMensaje == TipoMensaje.Exito ? resultado.Datos : null;
    }

    /// "Valor resumen" del QR (Anexo C, RS 113-2018/SUNAT) = ds:DigestValue del XML firmado, en base64 tal
    /// cual aparece en el nodo — no se recalcula acá, solo se extrae del XML que ya produjo el firmador.
    private static string? ExtraerDigestValue(byte[] xmlFirmado)
    {
        var documento = System.Xml.Linq.XDocument.Load(new MemoryStream(xmlFirmado));
        System.Xml.Linq.XNamespace ds = "http://www.w3.org/2000/09/xmldsig#";
        return documento.Descendants(ds + "DigestValue").FirstOrDefault()?.Value;
    }
}
