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

    // Único punto de logging para resultados que NO vinieron de una excepción (esas ya las loguea el catch
    // de EjecutarAsync): solo id=3 (ErrorSistema) — un fallo real de infraestructura que el Adaptador
    // atrapó y devolvió como envelope en vez de lanzar (ver AGENTS.md). id=1 (ReglaDeNegocio) nunca se
    // loguea acá: son resultados esperables (documento ya procesado, cuotas desalineadas, etc.), no fallas.
    private void LogSiErrorSistema(TipoMensaje idTipoMensaje, string mensaje, int idDocumentoElectronico, string contexto)
    {
        if (idTipoMensaje == TipoMensaje.ErrorSistema)
        {
            logger.LogError(
                "EnviarASunat — {Contexto}. idDocumentoElectronico={IdDocumentoElectronico}: {Mensaje}",
                contexto, idDocumentoElectronico, mensaje);
        }
    }

    private async Task<ResultadoOperacion<ResultadoEnvioSunat>> EjecutarInternoAsync(
        int idInquilino, int idDocumentoElectronico, string ambienteCodigo, CancellationToken cancellationToken)
    {
        var documento = await documentoRepositorio.ObtenerAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        if (documento.IdTipoMensaje != TipoMensaje.Exito || documento.Datos is null)
        {
            LogSiErrorSistema(documento.IdTipoMensaje, documento.Mensaje, idDocumentoElectronico, "falló al obtener el documento (lectura inicial)");
            return new ResultadoOperacion<ResultadoEnvioSunat>(documento.IdTipoMensaje, documento.Mensaje, default);
        }

        var cabecera = documento.Datos.Cabecera;

        if (!TiposDocumentoSoportados.Contains(cabecera.TipoDocumentoCodigo))
        {
            return ResultadoOperacion<ResultadoEnvioSunat>.DeReglaDeNegocio(
                "El Worker todavía no soporta el envío síncrono para este tipo de documento (solo Factura/Boleta/Nota de Crédito/Nota de Débito por ahora).");
        }

        if (cabecera.EstadoCodigo is not ("PendienteEnvio" or "Error"))
        {
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
                return ResultadoOperacion<ResultadoEnvioSunat>.DeReglaDeNegocio(
                    "La suma de las cuotas no coincide con el total del documento. Corrija las cuotas antes de confirmar con SUNAT.");
            }
        }

        // El borrador guarda una FechaEmision/HoraEmision inicial, pero la emisión real ocurre recién
        // ahora — se recalcula al momento de confirmar, no al guardar. SP_DocumentoElectronico_
        // ActualizarFechaEmision solo persiste exactamente estos dos campos (nada se recalcula del lado
        // del servidor), así que no hace falta releer el documento entero (cabecera+líneas+cuotas) para
        // obtener de vuelta un valor que ya tenemos en memoria — se parcha cabecera acá mismo.
        var ahora = RelojPeru.Ahora();
        var actualizacionFecha = await documentoRepositorio.ActualizarFechaEmisionAsync(
            UsuarioWorker, idInquilino, idDocumentoElectronico,
            DateOnly.FromDateTime(ahora), TimeOnly.FromDateTime(ahora), cancellationToken);
        if (actualizacionFecha.IdTipoMensaje != TipoMensaje.Exito)
        {
            LogSiErrorSistema(actualizacionFecha.IdTipoMensaje, actualizacionFecha.Mensaje, idDocumentoElectronico, "falló al actualizar fecha/hora de emisión");
            return new ResultadoOperacion<ResultadoEnvioSunat>(actualizacionFecha.IdTipoMensaje, actualizacionFecha.Mensaje, default);
        }

        cabecera = new DocumentoElectronico
        {
            IdDocumentoElectronico = cabecera.IdDocumentoElectronico,
            IdEmpresa = cabecera.IdEmpresa,
            IdExterno = cabecera.IdExterno,
            NumeroReferencia = cabecera.NumeroReferencia,
            TipoDocumentoCodigo = cabecera.TipoDocumentoCodigo,
            Serie = cabecera.Serie,
            Correlativo = cabecera.Correlativo,
            EstadoCodigo = cabecera.EstadoCodigo,
            FechaEmision = DateOnly.FromDateTime(ahora),
            HoraEmision = TimeOnly.FromDateTime(ahora),
            MonedaCodigo = cabecera.MonedaCodigo,
            TipoCambio = cabecera.TipoCambio,
            TipoOperacionCodigo = cabecera.TipoOperacionCodigo,
            FormaPagoCodigo = cabecera.FormaPagoCodigo,
            EmpresaRuc = cabecera.EmpresaRuc,
            EmpresaRazonSocial = cabecera.EmpresaRazonSocial,
            EmpresaNombreComercial = cabecera.EmpresaNombreComercial,
            EmpresaDireccion = cabecera.EmpresaDireccion,
            EmpresaUbigeo = cabecera.EmpresaUbigeo,
            ClienteTipoDocumentoCodigo = cabecera.ClienteTipoDocumentoCodigo,
            ClienteNumeroDocumento = cabecera.ClienteNumeroDocumento,
            ClienteNombre = cabecera.ClienteNombre,
            ClienteDireccion = cabecera.ClienteDireccion,
            ClienteCorreo = cabecera.ClienteCorreo,
            ClientePaisCodigo = cabecera.ClientePaisCodigo,
            TotalGravado = cabecera.TotalGravado,
            TotalInafecto = cabecera.TotalInafecto,
            TotalExonerado = cabecera.TotalExonerado,
            TotalExportacion = cabecera.TotalExportacion,
            TotalIgv = cabecera.TotalIgv,
            TotalIsc = cabecera.TotalIsc,
            TotalOtrosTributos = cabecera.TotalOtrosTributos,
            TotalDescuento = cabecera.TotalDescuento,
            TotalCargo = cabecera.TotalCargo,
            TotalImporte = cabecera.TotalImporte,
            SunatHash = cabecera.SunatHash,
            SunatCodigoRespuesta = cabecera.SunatCodigoRespuesta,
            SunatDescripcionRespuesta = cabecera.SunatDescripcionRespuesta,
            SunatTicket = cabecera.SunatTicket,
            FechaAceptacion = cabecera.FechaAceptacion,
            FechaRechazo = cabecera.FechaRechazo,
            FechaAnulacion = cabecera.FechaAnulacion,
            FchCre = cabecera.FchCre
        };
        documento = documento with { Datos = documento.Datos! with { Cabecera = cabecera } };

        // empresa/configuracion/claveSol no dependen entre sí — las tres solo necesitan idInquilino/
        // cabecera.IdEmpresa (y ambienteCodigo, ya conocido), así que se disparan juntas en vez de una
        // detrás de otra. certificado sí depende de configuracion.Datos.IdCertificado, así que se queda
        // secuencial después. El orden de los chequeos de abajo no cambia — mismo mensaje de error en
        // cada caso que antes, solo que las tres consultas ya corrieron en paralelo mientras tanto.
        var empresaTask = empresaRepositorio.ObtenerAsync(idInquilino, cabecera.IdEmpresa, cancellationToken);
        var configuracionTask = configuracionRepositorio.ObtenerPorEmpresaYAmbienteAsync(
            idInquilino, cabecera.IdEmpresa, ambienteCodigo, cancellationToken);
        var claveSolTask = credencialRepositorio.ObtenerPorTipoAsync(idInquilino, cabecera.IdEmpresa, "ClaveSol", cancellationToken);

        await Task.WhenAll(empresaTask, configuracionTask, claveSolTask);

        var empresa = await empresaTask;
        if (empresa.IdTipoMensaje != TipoMensaje.Exito || empresa.Datos is null)
        {
            LogSiErrorSistema(empresa.IdTipoMensaje, empresa.Mensaje, idDocumentoElectronico, "falló al obtener la empresa");
            return new ResultadoOperacion<ResultadoEnvioSunat>(empresa.IdTipoMensaje, empresa.Mensaje, default);
        }

        var configuracion = await configuracionTask;
        if (configuracion.IdTipoMensaje != TipoMensaje.Exito || configuracion.Datos is null)
        {
            LogSiErrorSistema(configuracion.IdTipoMensaje, configuracion.Mensaje, idDocumentoElectronico, "falló al obtener la configuración de facturación");
            return new ResultadoOperacion<ResultadoEnvioSunat>(configuracion.IdTipoMensaje, configuracion.Mensaje, default);
        }

        if (string.IsNullOrWhiteSpace(configuracion.Datos.UrlEnvioFacturaBoletaNota))
        {
            return ResultadoOperacion<ResultadoEnvioSunat>.DeReglaDeNegocio(
                "La configuración de facturación de la empresa no tiene URL de envío de Factura/Boleta/Nota.");
        }

        var certificado = await proveedorCertificado.ObtenerAsync(
            idInquilino, cabecera.IdEmpresa, configuracion.Datos.IdCertificado, cancellationToken);
        if (certificado.IdTipoMensaje != TipoMensaje.Exito || certificado.Datos is null)
        {
            LogSiErrorSistema(certificado.IdTipoMensaje, certificado.Mensaje, idDocumentoElectronico, "falló al obtener/cargar el certificado");
            return new ResultadoOperacion<ResultadoEnvioSunat>(certificado.IdTipoMensaje, certificado.Mensaje, default);
        }

        var claveSol = await claveSolTask;
        if (claveSol.IdTipoMensaje != TipoMensaje.Exito || claveSol.Datos is null)
        {
            LogSiErrorSistema(claveSol.IdTipoMensaje, claveSol.Mensaje, idDocumentoElectronico, "falló al obtener la credencial ClaveSol");
            return new ResultadoOperacion<ResultadoEnvioSunat>(claveSol.IdTipoMensaje, claveSol.Mensaje, default);
        }

        var claveSolDescifrada = await cifradoServicio.DescifrarAsync(
            idInquilino, claveSol.Datos.ValorCifrado, claveSol.Datos.Nonce, claveSol.Datos.Tag, cancellationToken);
        if (claveSolDescifrada.IdTipoMensaje != TipoMensaje.Exito || claveSolDescifrada.Datos is null)
        {
            LogSiErrorSistema(claveSolDescifrada.IdTipoMensaje, claveSolDescifrada.Mensaje, idDocumentoElectronico, "falló al descifrar la ClaveSol");
            return new ResultadoOperacion<ResultadoEnvioSunat>(claveSolDescifrada.IdTipoMensaje, claveSolDescifrada.Mensaje, default);
        }

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

        // Xml y Zip no dependen entre sí (cada uno ya tiene sus bytes calculados) — se suben a S3 + se
        // registran en ARCHIVOS_DOCUMENTO en paralelo en vez de uno detrás del otro.
        var archivoXmlTask = GuardarYRegistrarArchivoAsync(
            idInquilino, cabecera.IdDocumentoElectronico, carpeta, $"{nombreAlmacenamiento}.xml", xmlFirmado, "Xml", "application/xml", cancellationToken);
        var archivoZipTask = GuardarYRegistrarArchivoAsync(
            idInquilino, cabecera.IdDocumentoElectronico, carpeta, $"{nombreAlmacenamiento}.zip", zipBytes, "Zip", "application/zip", cancellationToken);

        await Task.WhenAll(archivoXmlTask, archivoZipTask);
        var idArchivoXml = await archivoXmlTask;
        var idArchivoZip = await archivoZipTask;

        // Reserva previa al envío, para los 4 tipos de documento: marca el documento como Enviando antes de
        // llamar a SUNAT, así un reintento concurrente del mismo documento (p.ej. un timeout del lado del
        // llamador que no cancela el procesamiento del lado del servidor) ve EstadoCodigo != PendienteEnvio
        // y no vuelve a enviarlo en paralelo. Para Nota de Crédito/Débito además revalida (bajo lock) que el
        // documento afectado siga Aceptado y no anulado, y la moneda — Insertar/GuardarCambios ya lo
        // validaron al guardar, pero el documento afectado pudo cambiar de estado/moneda después mientras
        // la Nota seguía PendienteEnvio. Para Nota de Crédito también revalida el saldo disponible: dos
        // borradores PendienteEnvio contra la misma Factura pueden pasar esa validación individualmente y
        // sobre-acreditar igual una vez que ambos se envían y SUNAT los acepta — la reserva hace que la
        // segunda vea el saldo ya tomado. Se ubica acá, después de armar/firmar/subir XML+ZIP pero antes de
        // registrar el intento de transmisión y de sendBill — el punto más tardío posible antes del envío
        // real, sin dejar un intento de transmisión abierto si termina rechazado acá.
        var reserva = await documentoRepositorio.ValidarSaldoNotaCreditoAsync(UsuarioWorker, idInquilino, cabecera.IdDocumentoElectronico, cancellationToken);
        if (reserva.IdTipoMensaje != TipoMensaje.Exito)
        {
            LogSiErrorSistema(reserva.IdTipoMensaje, reserva.Mensaje, idDocumentoElectronico, "el documento no pasó la reserva previa al envío");
            return new ResultadoOperacion<ResultadoEnvioSunat>(reserva.IdTipoMensaje, reserva.Mensaje, default);
        }

        var usuarioSolCompleto = empresa.Datos.Ruc + claveSol.Datos.Usuario;

        var nuevaTransmision = new NuevaTransmisionSunat(
            cabecera.IdDocumentoElectronico, null, configuracion.Datos.TipoProveedorCodigo,
            configuracion.Datos.UrlEnvioFacturaBoletaNota, "sendBill", idArchivoZip, 1, idArchivoXml);

        var transmision = await transmisionRepositorio.InsertarAsync(UsuarioWorker, idInquilino, nuevaTransmision, cancellationToken);
        if (transmision.IdTipoMensaje != TipoMensaje.Exito)
        {
            LogSiErrorSistema(transmision.IdTipoMensaje, transmision.Mensaje, idDocumentoElectronico, "falló al registrar el intento de transmisión");
            return new ResultadoOperacion<ResultadoEnvioSunat>(transmision.IdTipoMensaje, transmision.Mensaje, default);
        }

        var envio = await sunatCliente.EnviarAsync(
            configuracion.Datos.UrlEnvioFacturaBoletaNota, usuarioSolCompleto, claveSolDescifrada.Datos, nombreArchivoZip, zipBytes, cancellationToken);

        if (envio.IdTipoMensaje != TipoMensaje.Exito || envio.Datos is null)
        {
            LogSiErrorSistema(envio.IdTipoMensaje, envio.Mensaje, idDocumentoElectronico, "sendBill falló");

            await transmisionRepositorio.ActualizarAsync(
                UsuarioWorker, idInquilino, transmision.Datos,
                new ResultadoTransmisionSunat(EstadoMaestroCodigo.ErrorSunat, null, null, null, envio.IdTipoMensaje.ToString(), envio.Mensaje),
                cancellationToken);

            // ReglaDeNegocio = SUNAT sí respondió, solo que sin CDR usable (fault, HTTP de error, respuesta
            // sin applicationResponse) — eso es un hecho real sobre el documento, se marca ErrorSunat.
            // ErrorSistema = nunca hubo respuesta de SUNAT (ver el catch de SunatBillServiceCliente.EnviarAsync:
            // excepción de red/TLS/DNS antes de completar el request-response). Eso es un problema de
            // nuestro propio código/infraestructura, no un hecho sobre el documento — se deja en
            // PendienteEnvio para reintentar sin necesidad de "recuperarse" de nada. Esto además libera la
            // reserva (Enviando) que dejó ValidarSaldoNotaCreditoAsync arriba, para los 4 tipos de documento
            // — si no se revierte, el documento quedaría "en vuelo" para siempre sin haberse enviado nunca.
            if (envio.IdTipoMensaje == TipoMensaje.ReglaDeNegocio)
            {
                await documentoRepositorio.ActualizarEstadoSunatAsync(
                    UsuarioWorker, idInquilino, cabecera.IdDocumentoElectronico, EstadoMaestroCodigo.ErrorSunat,
                    null, null, envio.Mensaje, null, RelojPeru.Ahora(), cancellationToken);

                await errorRepositorio.InsertarAsync(
                    UsuarioWorker, idInquilino,
                    new ErrorDocumento(cabecera.IdDocumentoElectronico, transmision.Datos, "Sunat", string.Empty, envio.Mensaje, null, "Error"),
                    cancellationToken);
            }
            else
            {
                await documentoRepositorio.ActualizarEstadoSunatAsync(
                    UsuarioWorker, idInquilino, cabecera.IdDocumentoElectronico, EstadoMaestroCodigo.PendienteEnvio,
                    null, null, null, null, RelojPeru.Ahora(), cancellationToken);
            }

            return new ResultadoOperacion<ResultadoEnvioSunat>(envio.IdTipoMensaje, envio.Mensaje, default);
        }

        // El update de estado se adelanta a este punto (antes iba al final, después de Cdr/Pdf) —
        // SP_DocumentoElectronico_ObtenerTokenPublico (llamado dentro de ConstruirYGuardarPdfAsync) solo
        // devuelve el token cuando EstadoCodigo ya es Aceptado/AceptadoConObservaciones (cambio del
        // 12/08/2026 en ese SP); con el update al final, el documento seguía figurando "Enviando" en la
        // base justo cuando se pedía el token, así que el Pdf nunca se generaba durante sendBill.
        await documentoRepositorio.ActualizarEstadoSunatAsync(
            UsuarioWorker, idInquilino, cabecera.IdDocumentoElectronico, envio.Datos.EstadoCodigo,
            sunatHash, envio.Datos.SunatCodigoRespuesta, envio.Datos.SunatDescripcionRespuesta, null,
            RelojPeru.Ahora(), cancellationToken);

        // Cdr y Pdf tampoco dependen entre sí (el Pdf depende del token público + los bytes del CDR ya
        // firmados, no del registro del CDR en ARCHIVOS_DOCUMENTO) — mismo criterio que Xml/Zip arriba:
        // se disparan juntos en vez de esperar el CDR antes de siquiera empezar a construir el Pdf.
        var archivoCdrTask = GuardarYRegistrarArchivoAsync(
            idInquilino, cabecera.IdDocumentoElectronico, carpeta, $"{nombreAlmacenamiento}.cdr", envio.Datos.CdrXmlBytes, "Cdr", "application/xml", cancellationToken);

        var archivoPdfTask = envio.Datos.EstadoCodigo is EstadoMaestroCodigo.Aceptado or EstadoMaestroCodigo.AceptadoConObservaciones
            ? ConstruirYGuardarPdfAsync(idInquilino, cabecera, documento.Datos, empresa.Datos, sunatHash, carpeta, nombreAlmacenamiento, cancellationToken)
            : Task.FromResult<int?>(null);

        await Task.WhenAll(archivoCdrTask, archivoPdfTask);
        var idArchivoCdr = await archivoCdrTask;
        var idArchivoPdf = await archivoPdfTask;

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

        return ResultadoOperacion<ResultadoEnvioSunat>.DeExito("Documento procesado por SUNAT.", envio.Datos);
    }

    private async Task<int?> ConstruirYGuardarPdfAsync(
        int idInquilino, DocumentoElectronico cabecera, DocumentoElectronicoDetalle datosDocumento, Empresa empresa,
        string? sunatHash, string carpeta, string nombreAlmacenamiento, CancellationToken cancellationToken)
    {
        var tokenPublico = await documentoRepositorio.ObtenerTokenPublicoAsync(idInquilino, cabecera.IdDocumentoElectronico, cancellationToken);
        if (tokenPublico.IdTipoMensaje != TipoMensaje.Exito || tokenPublico.Datos is null)
        {
            return null;
        }

        var pdfBytes = generadorPdf.Construir(datosDocumento, empresa, tokenPublico.Datos, sunatHash);
        return await GuardarYRegistrarArchivoAsync(
            idInquilino, cabecera.IdDocumentoElectronico, carpeta, $"{nombreAlmacenamiento}.pdf", pdfBytes, "Pdf", "application/pdf", cancellationToken);
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

        // No se propaga como falla del envío completo (el archivo ya está en S3, y el resto del flujo puede
        // seguir sin este registro) — pero antes esto se perdía en silencio: el envío a SUNAT seguía como
        // si nada, dejando ARCHIVOS_DOCUMENTO desincronizado sin ningún rastro.
        LogSiErrorSistema(resultado.IdTipoMensaje, resultado.Mensaje, idDocumentoElectronico,
            $"se guardó {nombreArchivo} en S3 pero falló registrar el archivo en la base de datos");

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
